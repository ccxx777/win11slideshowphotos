from __future__ import annotations

from collections import deque
from pathlib import Path
from typing import Deque

from PySide6 import QtCore, QtGui


class _ImageLoadSignals(QtCore.QObject):
    loaded = QtCore.Signal(object, object)


class _LoadRunnable(QtCore.QRunnable):
    def __init__(self, path: Path, signals: _ImageLoadSignals) -> None:
        super().__init__()
        self._path = path
        self._signals = signals

    def run(self) -> None:
        reader = QtGui.QImageReader(str(self._path))
        reader.setAutoTransform(True)
        image = reader.read()
        if image.isNull():
            image = None
        self._signals.loaded.emit(self._path, image)


class ImageCache(QtCore.QObject):
    """Async preload cache to reduce black frames."""

    image_loaded = QtCore.Signal(object)

    def __init__(self, max_items: int = 4) -> None:
        super().__init__()
        self._max_items = max_items
        self._queue: Deque[Path] = deque()
        self._cache: dict[Path, QtGui.QImage] = {}
        self._pending: set[Path] = set()
        self._thread_pool = QtCore.QThreadPool.globalInstance()

    @property
    def max_items(self) -> int:
        return self._max_items

    def set_max_items(self, value: int) -> None:
        self._max_items = max(0, int(value))
        self._trim()

    def request(self, paths: list[Path]) -> None:
        for path in paths:
            if path in self._cache or path in self._pending:
                continue
            self._pending.add(path)
            signals = _ImageLoadSignals()
            signals.loaded.connect(self._handle_loaded)
            self._thread_pool.start(_LoadRunnable(path, signals))

    def get(self, path: Path) -> QtGui.QImage | None:
        return self._cache.get(path)

    def _handle_loaded(self, path: Path, image: QtGui.QImage | None) -> None:
        self._pending.discard(path)
        if image is None:
            return
        self._cache[path] = image
        self._queue.append(path)
        self._trim()
        self.image_loaded.emit(path)

    def _trim(self) -> None:
        while len(self._queue) > self._max_items:
            old = self._queue.popleft()
            self._cache.pop(old, None)
