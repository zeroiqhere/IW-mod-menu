"""Stop the feature owner when its menu or session becomes unavailable."""


class SessionHealth:
    def __init__(self, process_id: int, generation: int, now: float):
        self.process_id = process_id
        self.generation = generation
        self.last_progress = now
        self.started = now

    def observe(self, state: dict | None, renderer: dict | None,
                runtime_error: dict | None, now: float) -> None:
        # A stopped input worker cannot leave feature controls silently unreachable
        if runtime_error:
            if runtime_error.get("processId") != self.process_id:
                raise RuntimeError("The input runtime reported a foreign session identity")
            raise RuntimeError("The menu input worker stopped: " + str(runtime_error.get("error", "unknown error")))
        if not state or state.get("processId") != self.process_id:
            raise RuntimeError("The background controller lost its session identity")
        generation = state.get("generation")
        if type(generation) is not int or generation < self.generation:
            raise RuntimeError("The background controller heartbeat is invalid")
        if generation > self.generation:
            self.generation = generation
            self.last_progress = now
        elif now - self.last_progress > 3:
            raise RuntimeError("The background controller heartbeat stopped")
        # Closed menus need no rendered frame, but the diagnostic worker must be present
        if renderer is None:
            if now - self.started > 3:
                raise RuntimeError("The DLL did not publish graphics diagnostics")
            return
        if renderer.get("processId") != self.process_id:
            raise RuntimeError("The renderer reported a foreign session identity")
        faults = renderer.get("faults")
        if type(faults) is not int or faults < 0:
            raise RuntimeError("The renderer diagnostics are invalid")
        # A graphics fault requests controller cleanup rather than unloading live hooks
        if faults:
            raise RuntimeError("The menu renderer stopped drawing; restart the game before another attempt")
