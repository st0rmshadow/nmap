"""Scrollable monospace output view for live nmap stdout."""

from __future__ import annotations

import gi

gi.require_version("Gtk", "4.0")

from gi.repository import Gtk


class OutputView(Gtk.Box):
    def __init__(self) -> None:
        super().__init__(orientation=Gtk.Orientation.VERTICAL, spacing=0)

        self._find_matches: list[tuple[int, int]] = []
        self._find_index = 0

        self._find_revealer = Gtk.Revealer()
        self._find_revealer.set_reveal_child(False)
        self._find_revealer.set_transition_type(Gtk.RevealerTransitionType.SLIDE_DOWN)

        find_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
        find_box.set_margin_top(8)
        find_box.set_margin_bottom(8)
        find_box.set_margin_start(12)
        find_box.set_margin_end(12)
        find_box.add_css_class("toolbar")

        find_icon = Gtk.Image.new_from_icon_name("edit-find-symbolic")
        find_box.append(find_icon)

        self._find_entry = Gtk.Entry()
        self._find_entry.set_placeholder_text("Find in output")
        self._find_entry.set_hexpand(True)
        self._find_entry.connect("changed", self._on_find_text_changed)
        self._find_entry.connect("activate", lambda *_: self.find_next())
        find_box.append(self._find_entry)

        self._find_summary = Gtk.Label(label="")
        self._find_summary.add_css_class("dim-label")
        find_box.append(self._find_summary)

        prev_button = Gtk.Button.new_from_icon_name("go-up-symbolic")
        prev_button.set_tooltip_text("Previous Match")
        prev_button.connect("clicked", lambda *_: self.find_previous())
        find_box.append(prev_button)
        self._find_prev_button = prev_button

        next_button = Gtk.Button.new_from_icon_name("go-down-symbolic")
        next_button.set_tooltip_text("Next Match")
        next_button.connect("clicked", lambda *_: self.find_next())
        find_box.append(next_button)
        self._find_next_button = next_button

        close_button = Gtk.Button.new_from_icon_name("window-close-symbolic")
        close_button.set_tooltip_text("Close Find")
        close_button.add_css_class("flat")
        close_button.connect("clicked", lambda *_: self.set_find_visible(False))
        find_box.append(close_button)

        self._find_revealer.set_child(find_box)
        self.append(self._find_revealer)

        scrolled = Gtk.ScrolledWindow()
        scrolled.set_vexpand(True)
        scrolled.set_policy(Gtk.PolicyType.AUTOMATIC, Gtk.PolicyType.AUTOMATIC)

        self._text_view = Gtk.TextView()
        self._text_view.set_editable(False)
        self._text_view.set_monospace(True)
        self._text_view.set_wrap_mode(Gtk.WrapMode.WORD_CHAR)
        self._text_buffer = self._text_view.get_buffer()
        self._find_tag = self._text_buffer.create_tag("zenmap-find-match", background="#f6e05e")
        self._find_current_tag = self._text_buffer.create_tag(
            "zenmap-find-current",
            background="#ed8936",
            foreground="#1a202c",
        )
        scrolled.set_child(self._text_view)

        self.append(scrolled)

    def set_text(self, text: str) -> None:
        self._text_buffer.set_text(text)
        if self._find_revealer.get_reveal_child():
            self._refresh_find_matches()

    def append_text(self, text: str) -> None:
        end_iter = self._text_buffer.get_end_iter()
        self._text_buffer.insert(end_iter, text)
        mark = self._text_buffer.create_mark(None, end_iter, False)
        self._text_view.scroll_to_mark(mark, 0.0, False, 0.0, 1.0)
        if self._find_revealer.get_reveal_child() and self._find_entry.get_text().strip():
            self._refresh_find_matches(preserve_index=True)

    def clear(self) -> None:
        self._text_buffer.set_text("")
        self._clear_find_highlights()
        self._find_matches = []
        self._find_index = 0
        self._update_find_summary()

    def set_find_visible(self, visible: bool) -> None:
        self._find_revealer.set_reveal_child(visible)
        if visible:
            self._find_entry.grab_focus()
            self._refresh_find_matches()
        else:
            self._find_entry.set_text("")
            self._clear_find_highlights()
            self._find_matches = []
            self._find_index = 0
            self._update_find_summary()

    def toggle_find(self) -> None:
        self.set_find_visible(not self._find_revealer.get_reveal_child())

    def find_next(self) -> None:
        if not self._find_matches:
            return
        self._find_index = (self._find_index + 1) % len(self._find_matches)
        self._focus_current_match()

    def find_previous(self) -> None:
        if not self._find_matches:
            return
        self._find_index = (self._find_index - 1 + len(self._find_matches)) % len(self._find_matches)
        self._focus_current_match()

    def _on_find_text_changed(self, _entry: Gtk.Entry) -> None:
        self._find_index = 0
        self._refresh_find_matches()

    def _refresh_find_matches(self, *, preserve_index: bool = False) -> None:
        query = self._find_entry.get_text().strip()
        self._clear_find_highlights()
        previous_index = self._find_index if preserve_index else 0
        self._find_matches = []

        if not query:
            self._find_index = 0
            self._update_find_summary()
            return

        start = self._text_buffer.get_start_iter()
        while True:
            found = start.forward_search(query, Gtk.TextSearchFlags.CASE_INSENSITIVE, None)
            if found is None:
                break
            match_start, match_end = found
            self._text_buffer.apply_tag(self._find_tag, match_start, match_end)
            self._find_matches.append((match_start.get_offset(), match_end.get_offset()))
            start = match_end

        if self._find_matches:
            self._find_index = min(previous_index, len(self._find_matches) - 1)
            self._focus_current_match()
        else:
            self._find_index = 0
            self._update_find_summary()

    def _focus_current_match(self) -> None:
        self._clear_find_highlights(keep_matches=True)
        if not self._find_matches:
            self._update_find_summary()
            return

        start_offset, end_offset = self._find_matches[self._find_index]
        start = self._text_buffer.get_iter_at_offset(start_offset)
        end = self._text_buffer.get_iter_at_offset(end_offset)
        self._text_buffer.apply_tag(self._find_current_tag, start, end)
        self._text_buffer.select_range(start, end)
        self._text_view.scroll_to_iter(start, 0.2, True, 0.0, 0.3)
        self._update_find_summary()

    def _clear_find_highlights(self, *, keep_matches: bool = False) -> None:
        start = self._text_buffer.get_start_iter()
        end = self._text_buffer.get_end_iter()
        self._text_buffer.remove_tag(self._find_current_tag, start, end)
        if not keep_matches:
            self._text_buffer.remove_tag(self._find_tag, start, end)

    def _update_find_summary(self) -> None:
        query = self._find_entry.get_text().strip()
        count = len(self._find_matches)
        if not query:
            self._find_summary.set_label("")
        elif count == 0:
            self._find_summary.set_label("No matches")
        else:
            self._find_summary.set_label(f"{self._find_index + 1} of {count}")

        has_matches = count > 0
        self._find_prev_button.set_sensitive(has_matches)
        self._find_next_button.set_sensitive(has_matches)
