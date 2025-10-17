Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

# Function to get all visible window titles
function Get-WindowTitles {
    $titles = New-Object System.Collections.Generic.List[string]

    $callback = [Win32+EnumWindowsProc]{
        param($hWnd, $lParam)
        if ([Win32]::IsWindowVisible($hWnd)) {
            $sb = New-Object System.Text.StringBuilder 1024
            [Win32]::GetWindowText($hWnd, $sb, $sb.Capacity) | Out-Null
            $title = $sb.ToString()
            if (![string]::IsNullOrWhiteSpace($title)) {
                $titles.Add($title)
            }
        }
        return $true
    }

    [Win32]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    return $titles
}

# Example usage:
# Get all window titles that contain a specific string (case-insensitive)
$search = "vq0"  # change this to what you’re looking for
Get-WindowTitles | Where-Object { $_ -match $search }