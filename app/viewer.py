from __future__ import annotations

from pathlib import Path

from PySide6 import QtCore, QtGui, QtWidgets

from .image_cache import ImageCache


class SlideShowWindow(QtWidgets.QMainWindow):
    def __init__(self, groups: list[list[Path]], interval_seconds: float, preload_count: int) -> None:
        super().__init__()
        self.setWindowTitle("Win11 Slideshow Photos")
        self._groups = groups
        self._group_index = 0
        self._image_index = 0
        self._zoom = 1.0
        self._last_pixmap: QtGui.QPixmap | None = None

        self._label = QtWidgets.QLabel("", alignment=QtCore.Qt.AlignCenter)
        self._label.setMinimumSize(640, 360)
        self.setCentralWidget(self._label)

        self._cache = ImageCache(preload_count)
        self._cache.image_loaded.connect(self._on_image_loaded)
        self._timer = QtCore.QTimer(self)
        self._timer.timeout.connect(self.next_image)

        self._interval_spin = QtWidgets.QDoubleSpinBox()
        self._interval_spin.setRange(0.05, 60.0)
        self._interval_spin.setDecimals(2)
        self._interval_spin.setSingleStep(0.05)
        self._interval_spin.setValue(max(0.05, float(interval_seconds)))
        self._interval_spin.valueChanged.connect(self._on_interval_changed)

        toolbar = QtWidgets.QToolBar("Controls")
        toolbar.setMovable(False)
        toolbar.addWidget(QtWidgets.QLabel("Interval (s): "))
        toolbar.addWidget(self._interval_spin)
        self.addToolBar(toolbar)

        self._apply_interval(self._interval_spin.value())

        if not self._groups:
            self._label.setText("No images found. Update app/settings.py")
        else:
            self._show_current()

    def wheelEvent(self, event: QtGui.QWheelEvent) -> None:
        modifiers = event.modifiers()
        delta = event.angleDelta().y()
        if modifiers & QtCore.Qt.ControlModifier:
            step = 0.1 if delta > 0 else -0.1
            self._zoom = max(0.1, min(5.0, self._zoom + step))
            self._render()
        else:
            if delta > 0:
                self.prev_image()
            else:
                self.next_image()

    def resizeEvent(self, event: QtGui.QResizeEvent) -> None:
        super().resizeEvent(event)
        self._render()

    def next_image(self) -> None:
        if not self._groups:
            return

        if self._image_index + 1 < len(self._groups[self._group_index]):
            self._image_index += 1
        else:
            self._group_index = (self._group_index + 1) % len(self._groups)
            self._image_index = 0

        self._show_current()

    def prev_image(self) -> None:
        if not self._groups:
            return

        if self._image_index > 0:
            self._image_index -= 1
        else:
            self._group_index = (self._group_index - 1) % len(self._groups)
            self._image_index = len(self._groups[self._group_index]) - 1

        self._show_current()

    def _show_current(self) -> None:
        self._request_images()
        self._render()

    def _request_images(self) -> None:
        if not self._groups:
            return
        current = self._current_path()
        lookahead = self._collect_forward_paths(self._cache.max_items)
        self._cache.request([current, *lookahead])

    def _collect_forward_paths(self, count: int) -> list[Path]:
        paths: list[Path] = []
        if not self._groups or count <= 0:
            return paths

        gi = self._group_index
        ii = self._image_index
        for _ in range(count):
            if ii + 1 < len(self._groups[gi]):
                ii += 1
            else:
                gi = (gi + 1) % len(self._groups)
                ii = 0
            paths.append(self._groups[gi][ii])
        return paths

    def _current_path(self) -> Path:
        return self._groups[self._group_index][self._image_index]

    def _on_image_loaded(self, path: Path) -> None:
        if not self._groups:
            return
        if path == self._current_path():
            self._render()

    def _on_interval_changed(self, value: float) -> None:
        self._apply_interval(value)

    def _apply_interval(self, value: float) -> None:
        interval_ms = int(max(0.05, float(value)) * 1000)
        self._timer.start(interval_ms)

    def _render(self) -> None:
        if not self._groups:
            return
        path = self._current_path()
        image = self._cache.get(path)
        if image is None:
            if self._last_pixmap is None:
                self._label.setText(f"Loading: {path.name}")
            return

        pixmap = QtGui.QPixmap.fromImage(image)

        target_size = self._label.size()
        if self._zoom != 1.0:
            target_size = QtCore.QSize(int(target_size.width() * self._zoom), int(target_size.height() * self._zoom))

        scaled = pixmap.scaled(target_size, QtCore.Qt.KeepAspectRatio, QtCore.Qt.SmoothTransformation)
        self._label.setPixmap(scaled)
        self._last_pixmap = pixmap
