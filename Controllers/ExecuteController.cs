using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;
using System.IO.Compression;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecuteController : ControllerBase
{
    private readonly IExecutionService _executionService;
    private readonly ILogger<ExecuteController> _logger;

    public ExecuteController(IExecutionService executionService, ILogger<ExecuteController> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }

    [HttpPost("exec")]
    public async Task<ActionResult<ExecuteFileResponse>> ExecuteFile([FromForm] IFormFile file, 
        [FromForm] string? path = null, [FromForm] string? fileargs = null, [FromForm] string? executeFile = null)
    {
        try
        {
            // Validate file upload
            if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(file.FileName))
            {
                _logger.LogWarning("Data error: file content size: {Size}, filename: {FileName}", 
                    file?.Length ?? 0, file?.FileName ?? "");
                
                return BadRequest(new ExecuteFileResponse
                {
                    Status = "error",
                    Message = "Invalid request: filename or file data is missing"
                });
            }

            // Determine path
            var targetPath = string.IsNullOrWhiteSpace(path) ? @"C:\RedEdr\data\" : path;
            if (!targetPath.EndsWith(@"\"))
            {
                targetPath += @"\";
            }
            var filePath = Path.Combine(targetPath, file.FileName);

            // Read file content
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Write the malware (always write first, whether ZIP or executable)
            _logger.LogInformation("Writing file: {FilePath}", filePath);
            if (!await _executionService.WriteMalwareAsync(filePath, fileContent))
            {
                _logger.LogError("Failed to write file to {FilePath}", filePath);
                return StatusCode(500, new ExecuteFileResponse
                {
                    Status = "error",
                    Message = "Failed to write file"
                });
            }

            // Check if file is ZIP or RAR and handle extraction
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string actualFilePath = filePath;
            
            if (fileExtension == ".zip" || fileExtension == ".rar")
            {
                _logger.LogInformation("Detected archive file: {FileName}, extracting to temp directory", file.FileName);
                
                // Create extraction directory in user's temp folder
                var tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", Path.GetRandomFileName());
                Directory.CreateDirectory(tempPath);
                _logger.LogInformation("Created extraction directory: {TempPath}", tempPath);

                if (fileExtension == ".zip")
                {
                    // Extract ZIP file from the written file
                    using (var zip = ZipFile.OpenRead(filePath))
                    {
                        zip.ExtractToDirectory(tempPath, overwriteFiles: true);
                    }
                    _logger.LogInformation("Successfully extracted ZIP file to: {TempPath}", tempPath);
                }
                else if (fileExtension == ".rar")
                {
                    // For RAR files, we'll need to use an external tool
                    // For now, we'll return an error as RAR support requires additional libraries
                    _logger.LogError("RAR files are not yet supported");
                    return BadRequest(new ExecuteFileResponse
                    {
                        Status = "error",
                        Message = "RAR files are not yet supported. Please use ZIP files instead."
                    });
                }

                // Find the file to execute
                string? fileToExecute = null;
                var executableExtensions = new[] { ".exe", ".bat", ".com", ".lnk" };

                if (!string.IsNullOrWhiteSpace(executeFile))
                {
                    // Use specified file
                    var specifiedFilePath = Path.Combine(tempPath, executeFile);
                    if (System.IO.File.Exists(specifiedFilePath))
                    {
                        fileToExecute = specifiedFilePath;
                        _logger.LogInformation("Using specified file for execution: {ExecuteFile}", executeFile);
                    }
                    else
                    {
                        _logger.LogError("Specified file not found in archive: {ExecuteFile}", executeFile);
                        return BadRequest(new ExecuteFileResponse
                        {
                            Status = "error",
                            Message = $"Specified file '{executeFile}' not found in archive"
                        });
                    }
                }
                else
                {
                    // Find alphabetically first executable file
                    var allFiles = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);
                    var executableFiles = allFiles
                        .Where(f => executableExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => Path.GetFileName(f))
                        .ToList();

                    if (executableFiles.Any())
                    {
                        fileToExecute = executableFiles.First();
                        _logger.LogInformation("Selected alphabetically first executable: {FileName}", Path.GetFileName(fileToExecute));
                    }
                    else
                    {
                        _logger.LogError("No executable files found in archive");
                        return BadRequest(new ExecuteFileResponse
                        {
                            Status = "error",
                            Message = "No executable files (.exe, .bat, .com, .lnk) found in archive"
                        });
                    }
                }

                actualFilePath = fileToExecute;
            }

            // Start the malware (use actualFilePath which might be extracted file or original file)
            _logger.LogInformation("Executing file: {FilePath}", actualFilePath);
            var (success, pid, errorMessage) = await _executionService.StartProcessAsync(actualFilePath, fileargs);
            
            if (!success)
            {
                if (errorMessage == "virus")
                {
                    _logger.LogInformation("Malware execution blocked by antivirus");
                    return Ok(new ExecuteFileResponse
                    {
                        Status = "virus",
                        Pid = pid
                    });
                }

                return StatusCode(500, new ExecuteFileResponse
                {
                    Status = "error",
                    Message = $"Failed to execute malware: {errorMessage}"
                });
            }

            return Ok(new ExecuteFileResponse
            {
                Status = "ok",
                Pid = pid
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /api/exec");
            return StatusCode(500, new ExecuteFileResponse
            {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }

    [HttpPost("kill")]
    public async Task<ActionResult<KillResponse>> KillLastExecution()
    {
        try
        {
            _logger.LogInformation("Kill request received");
            
            var (success, errorMessage) = await _executionService.KillLastExecutionAsync();
            
            if (!success)
            {
                return StatusCode(500, new KillResponse
                {
                    Status = "error",
                    Message = errorMessage ?? "Failed to kill last execution"
                });
            }

            return Ok(new KillResponse
            {
                Status = "ok",
                Message = errorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /api/kill");
            return StatusCode(500, new KillResponse
            {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }
}
