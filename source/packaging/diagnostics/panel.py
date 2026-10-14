"""A harmless menu model with no feature action dispatch."""

from dataclasses import dataclass
from .protocol import Command, MAXIMUM_SEQUENCE

ROWS = (
    ("Renderer diagnostic", "DISPLAY ONLY"),
    ("Personal Godmode", "UNAVAILABLE"),
    ("Lobby Infinite Ammo", "UNAVAILABLE"),
    ("Teammate Godmode", "UNAVAILABLE"),
)


@dataclass
class DiagnosticPanel:
    process_id: int
    generation: int = 0
    last_sequence: int = 0
    is_open: bool = False
    selected_index: int = 0

    def consume(self, command: Command | None) -> bool:
        if command is None or command.sequence <= self.last_sequence:
            return False
        self.last_sequence = command.sequence
        if not command.valid:
            return True
        # Selection never calls another module or changes a game field
        if command.input == "toggle":
            self.is_open = not self.is_open
        elif command.input == "back":
            self.is_open = False
        elif self.is_open and command.input == "up":
            self.selected_index = (self.selected_index - 1) % len(ROWS)
        elif self.is_open and command.input == "down":
            self.selected_index = (self.selected_index + 1) % len(ROWS)
        return True

    def snapshot(self, *, closed: bool = False, notice: str | None = None) -> dict:
        if self.generation == MAXIMUM_SEQUENCE:
            raise ValueError("The diagnostic heartbeat counter is exhausted")
        # Every row is informational and permanently unavailable to feature selection
        return {"version": 1, "processId": self.process_id, "generation": self.generation + 1,
                "lastSequence": self.last_sequence, "isOpen": self.is_open and not closed,
                "accent": "#3998FE", "title": "APPARITION DIAGNOSTIC",
                "context": "Renderer only | features unavailable",
                "notice": notice or "Aim + melee opens. Melee closes. Aim / fire scroll. Use selects nothing.",
                "selectedIndex": self.selected_index,
                "rows": [{"label": label, "value": value, "enabled": False} for label, value in ROWS]}
