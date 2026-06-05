import argparse
import os
import random
import sys
import tempfile
import time
import requests
from colorama import init, Fore

# Initialize colorama for cross-platform colored terminal output
init(autoreset=True)

def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Simple workflow script for DetonatorAgent",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    
    parser.add_argument("--file", required=True, help="Path to the file to execute")
    parser.add_argument("--droppath", default=r"C:\Users\Public\Downloads\\", help="Target directory to write the file")
    parser.add_argument("--executableargs", default="", help="Arguments to pass to the executable")
    parser.add_argument("--executablename", default="", help="Specific file to execute from an archive")
    parser.add_argument("--executionmode", default="autoit", choices=["exec", "autoit", "clickfix"], help="Execution service type")
    parser.add_argument("--runtime", type=int, default=10, help="Duration in seconds to wait before killing the process")
    parser.add_argument("--server", default="http://localhost:8080", help="Base URL of the DetonatorAgent API")
    
    return parser.parse_args()


def get_edr_alerts(base_url, sleep_time=1, count=1):
    print(f"Poll for alerts every second for {count} seconds...\n")
    
    seen_alerts = {}
    header_displayed = False
    
    for iteration in range(count):
        try:
            response = requests.get(f"{base_url}/api/logs/edr", timeout=5)
            if response.status_code == 200:
                edr_response = response.json()
                alerts = edr_response.get("alerts", [])
                
                if alerts:
                    for alert in alerts:
                        alert_id = alert.get("alertId")
                        if alert_id and alert_id not in seen_alerts:
                            seen_alerts[alert_id] = True
                            
                            # Display header only once
                            if not header_displayed:
                                print(f"{'title':<30} {'severity':<8} {'category':<8}")
                                print(f"{'-----':<30} {'--------':<8} {'--------':<8}")
                                header_displayed = True
                            
                            title = alert.get("title", "").ljust(30)
                            severity = alert.get("severity", "").ljust(8)
                            category = alert.get("category", "").ljust(8)
                            print(f"{title} {severity} {category}")
            else:
                print(Fore.YELLOW + f"  Warning: Failed to retrieve EDR logs (Status: {response.status_code})")
        except Exception as e:
            print(Fore.YELLOW + "  Warning: Failed to parse EDR logs")
            # If we got raw text but it wasn't JSON
            try:
                print(Fore.LIGHTBLACK_EX + f"  Response: {response.text}")
            except NameError:
                print(Fore.LIGHTBLACK_EX + f"  Response Error: {e}")
                
        # Sleep between iterations (but not after the last one)
        if sleep_time > 0 and iteration < (count - 1):
            time.sleep(sleep_time)


def main():
    args = parse_arguments()
    
    # Validate input file exists
    if not os.path.exists(args.file):
        print(Fore.RED + f"Error: File not found: {args.file}")
        sys.exit(1)
        
    # Acquire Lock
    try:
        lock_response = requests.post(f"{args.server}/api/lock/acquire", timeout=5)
        lock_response.raise_for_status()
    except Exception:
        print(Fore.RED + f"Error: Cant reach {args.server}")
        sys.exit(1)
        
    temp_file_path = None
    status = ""
    
    try:
        # Encrypt file via basic XOR
        xor_key = random.randint(1, 255)
        with open(args.file, "rb") as f:
            file_bytes = f.read()
            
        encrypted_bytes = bytes([b ^ xor_key for b in file_bytes])
        
        # Write encrypted file to temporary location
        with tempfile.NamedTemporaryFile(delete=False) as temp_file:
            temp_file.write(encrypted_bytes)
            temp_file_path = temp_file.name
            
        #print(Fore.LIGHTBLACK_EX + f"Encrypted file created: {temp_file_path}")
        
        # Prepare multipart/form-data payload
        file_name = os.path.basename(args.file)
        
        # requests requires a payload dictionary for data fields and files tuple for file streams
        payload = {
            "drop_path": args.droppath,
            "xor_key": str(xor_key)
        }
        if args.executableargs:
            payload["executable_args"] = args.executableargs
        if args.executablename:
            payload["executable_name"] = args.executablename
        if args.executionmode:
            payload["execution_mode"] = args.executionmode
            
        print("Executing file on DetonatorAgent...")
        
        with open(temp_file_path, "rb") as tf:
            files = {"file": (file_name, tf)}
            exec_response = requests.post(f"{args.server}/api/execute/exec", data=payload, files=files)
            
        if exec_response.status_code != 200:
            print(Fore.RED + f"Error: Failed to execute file (HTTP status code: {exec_response.status_code})")
            try:
                error_detail = exec_response.json()
                print(Fore.RED + f"Server error: {error_detail}")
            except Exception:
                if exec_response.text:
                    print(Fore.RED + f"Server error: {exec_response.text}")
            sys.exit(1)
            
        # Parse and check the execution response
        try:
            response_obj = exec_response.json()
            status = response_obj.get("status", "")  # "virus", "ok", "error"
        except Exception:
            print(Fore.RED + "Warning: Failed to parse execution response")
            print(Fore.LIGHTBLACK_EX + f"Response: {exec_response.text}")
            
        match status:
            case "virus":
                print(Fore.YELLOW + "File detected as malicious on write (not executed)")
            case "ok":
                print(Fore.GREEN + "File executed successfully")
            case "error":
                print(Fore.RED + "Error during execution")
            case _:
                print(Fore.RED + f"Unexpected status: {status}")
        
        # Cleanup temporary encrypted file
        if temp_file_path and os.path.exists(temp_file_path):
            os.remove(temp_file_path)
            
        # Wait & Poll (if execution was successful)
        if status == "ok":
            print(f"Execution running, waiting {args.runtime} seconds...")
            get_edr_alerts(base_url=args.server, sleep_time=1, count=args.runtime)
            print("Polling/Runtime finished")
            
        # If it's detected on file write, poll for 3 seconds to allow EDR to process
        if status == "virus":
            get_edr_alerts(base_url=args.server, sleep_time=1, count=3)
            
        # Kill process if it ran
        if status == "ok":
            #print("Killing process...")
            try:
                kill_response = requests.post(f"{args.server}/api/execute/kill", timeout=5)
                if kill_response.status_code != 200:
                    print(Fore.YELLOW + f"Warning: Failed to kill process (HTTP status code: {kill_response.status_code})")
            except Exception:
                print(Fore.YELLOW + "Warning: Failed to kill process")

    finally:
        # Release Lock (always execute)
        try:
            unlock_response = requests.post(f"{args.server}/api/lock/release", timeout=5)
            if unlock_response.status_code != 200:
                print(Fore.YELLOW + f"Warning: Failed to release lock (HTTP status code: {unlock_response.status_code})")
        except Exception:
            print(Fore.YELLOW + "Warning: Failed to release lock")


if __name__ == "__main__":
    main()