# Build single-file EXE with PyInstaller
$ErrorActionPreference = 'Stop'

$env:PYTHONIOENCODING = 'utf-8'

pyinstaller --noconfirm --clean --onefile --windowed `
  --name "Win11SlideshowPhotos" `
  .\app\__main__.py
