from worlds.LauncherComponents import Component, Type, components, launch as launch_component

from .world import CozyCleanerWorld as CozyCleanerWorld


def launch_client(*args: str) -> None:
    from .client import launch
    launch_component(launch, name="CozyCleanerClient", args=args)


components.append(Component("Cozy Cleaner Client", func=launch_client, component_type=Type.CLIENT,
                            game_name="Cozy Cleaner", supports_uri=True,
                            description="Connects Cozy Cleaner (BepInEx plugin bridge) to a multiworld."))
