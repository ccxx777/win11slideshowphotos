from __future__ import annotations

import sys
from pathlib import Path


def _resolve_main():
    try:
        from .main import main
        return main
    except ImportError:
        root = Path(__file__).resolve().parents[1]
        sys.path.insert(0, str(root))
        from app.main import main
        return main


if __name__ == "__main__":
    raise SystemExit(_resolve_main()())
