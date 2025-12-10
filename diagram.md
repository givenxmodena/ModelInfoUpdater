# Update System Workflow Diagram

This document shows the high-level workflow of the update system, from the moment the user clicks the **Update** button through checking GitHub releases, comparing versions, downloading and applying the update, and handling success/failure paths.

---

## Legend

- `[ ]` Rectangular boxes = process steps
- `< >` Angled boxes / split points = decisions or branches
- `→`, `↓` = flow direction
- Dashed lines or side branches = alternate paths (errors, cancellation)

---

## High-Level ASCII Flow Diagram

```text
+------------------------------------------------------+
|                User Interface Layer                  |
+------------------------------------------------------+
           |
           v
   +---------------------------+
   | User clicks "Update"     |
   +---------------------------+
           |
           v
   +---------------------------+
   | Disable Update button     |
   | Show "Checking..." state  |
   +---------------------------+
           |
           v
+------------------------------------------------------+
|           GitHub Release Check & Versioning          |
+------------------------------------------------------+
           |
           v
   +----------------------------------------------+
   | Build GitHub Releases API request            |
   |  GET /repos/{owner}/{repo}/releases/latest   |
   +----------------------------------------------+
           |
           v
   +---------------------------+
   | Call GitHub Releases API  |
   +---------------------------+
           |
     +-----+---------------------------+
     |                                 |
     v                                 v
+------------+                 +---------------------------+
| 200 OK     |                 | Network/API error        |
+------------+                 +---------------------------+
     |                           |  - timeout
     |                           |  - non-2xx status
     |                           |  - parse failure
     v                           +---------------------------+
+---------------------------+                |
| Parse JSON response       |                v
| Extract latest version    |       +------------------------------+
| (e.g. tag_name)           |       | Show error message to user   |
+---------------------------+       | Re-enable Update button      |
           |                       | Keep current version          |
           |                       +------------------------------+
           |                                      |
           |                                      v
           |                              (Update flow ends)
           v
+------------------------------------------------------+
|               Version Comparison Logic               |
+------------------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Determine current app version (local)      |
   | Normalize versions (e.g. semver parse)     |
   +--------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Compare: latest_version > current_version? |
   +--------------------------------------------+
           |
     +-----+------------+
     |                  |
     v                  v
  (Yes)             (No / Equal)
     |                  |
     |                  +---------------------------------------+
     |                  | Show "Already up to date" message    |
     |                  | Re-enable Update button               |
     |                  +---------------------------------------+
     |                                      |
     |                                      v
     |                              (Update flow ends)
     v
+------------------------------------------------------+
|                  Update Available Path               |
+------------------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Prompt user:                               |
   |  "A new version is available. Download?"  |
   +--------------------------------------------+
           |
     +-----+------------+
     |                  |
     v                  v
  (Accept)           (Cancel)
     |                  |
     |                  +---------------------------------------+
     |                  | Re-enable Update button               |
     |                  | Keep current version                  |
     |                  +---------------------------------------+
     |                                      |
     |                                      v
     |                              (Update flow ends)
     v
+------------------------------------------------------+
|                   Download New Version               |
+------------------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Resolve asset URL for latest release       |
   | (e.g. installer / archive download link)   |
   +--------------------------------------------+
           |
           v
   +---------------------------+
   | Start download            |
   | Show progress (optional)  |
   +---------------------------+
           |
     +-----+------------+
     |                  |
     v                  v
 (Success)          (Failure)
     |                  |
     |                  +---------------------------------------+
     |                  | Show "Download failed" message       |
     |                  | Re-enable Update button               |
     |                  | Keep current version                  |
     |                  +---------------------------------------+
     |                                      |
     |                                      v
     |                              (Update flow ends)
     v
+------------------------------------------------------+
|                  Installation / Apply Update         |
+------------------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Verify downloaded file (size/checksum)     |
   | (Optional: signature / hash check)         |
   +--------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Prepare for update:                        |
   |  - Close background tasks if required      |
   |  - Schedule self-update / external installer|
   +--------------------------------------------+
           |
           v
   +---------------------------+
   | Apply update / run        |
   | installer                 |
   +---------------------------+
           |
     +-----+------------+
     |                  |
     v                  v
 (Success)          (Failure)
     |                  |
     |                  +---------------------------------------+
     |                  | Show "Installation failed" message   |
     |                  | Log error details                    |
     |                  | Suggest retry or manual update       |
     |                  | Re-enable Update button              |
     |                  +---------------------------------------+
     |                                      |
     |                                      v
     |                              (Update flow ends)
     v
+------------------------------------------------------+
|                 Completion & Restart                 |
+------------------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Relaunch application with new version      |
   | (if external installer closed old app)     |
   +--------------------------------------------+
           |
           v
   +--------------------------------------------+
   | Show "Update complete" / release notes    |
   | Re-enable Update button                    |
   +--------------------------------------------+
           |
           v
      (Normal operation resumes)
```

