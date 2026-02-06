# Build single-file EXE with PyInstaller
$ErrorActionPreference = 'Stop'

$env:PYTHONIOENCODING = 'utf-8'

pyinstaller --noconfirm --clean --onefile --windowed `
  --name "Win11SlideshowPhotos" `
  --collect-all PySide6 `
  --collect-all shiboken6 `
  .\app\main.py
