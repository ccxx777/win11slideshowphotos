# win11slideshowphotos

Lightweight Windows 11 slideshow viewer (Python + PySide6) project skeleton.

## Quick start
1. Create venv
   - `python -m venv .venv`
2. Activate
   - PowerShell: `.\.venv\Scripts\Activate.ps1`
3. Install deps
   - `pip install -r requirements.txt`
4. Run
   - `python -m app`

## Package (single EXE)
1. Install build deps
   - `pip install -r requirements-dev.txt`
2. Build
   - `powershell -ExecutionPolicy Bypass -File .\build.ps1`
3. Output
   - `dist\Win11SlideshowPhotos.exe`

## Notes
- Edit `app/settings.py` to set the initial root folder.
- Folder chaining, interval control, and async preloading are implemented in the UI.
