# Onboarding

- `TutorialController`: scene entry, first-run state, real action tracking, pause rules and cleanup.
- `TutorialSteps`: twelve short story/action prompts. After the camera step, highlight disaster damage exposure and require opening any earthquake/flood/fire breakdown; these values describe potential damage severity, not occurrence probability.
- `TutorialView`: guide card, UI spotlight, world marker and skip/continue controls.

Each game first confirms city/mayor names (saved values are prefilled), then ElectionManager shows the election victory front page using the shared torn newspaper paper/grain and NYT masthead. Tutorial starts only after that front page is dismissed. GameSceneEntry ensures ElectionManager also exists when entering CityMap from the menu.
`Tutorial.Completed.v1` is saved only when finishing or skipping the tutorial. Settings contains a restart button; replay uses the current city and normal costs, without granting money or deleting progress.

Flow: welcome → camera → happiness breakdown → budget breakdown → population and employment breakdown (staff shortages reduce workplace income) → complete housing construction and highlight the actual before/after population change → inspect a building → dispatch and complete dry grass cleanup → build a fire station (or inspect existing coverage) → sign a curated newspaper policy → finish.

Reading and interaction setup freeze simulation. Relevant construction and assigned cleanup crews release it; completing them freezes it again. Camera movement remains available. Settings always pauses the tutorial's simulation. Random disasters, normal newspaper scheduling, automatic maintenance and news wait until onboarding ends. Disaster dates shift by the days spent training.

UI is nonblocking, with a spotlight around the target and a teal circle on the map. Missing plots/services do not trap the player: every action step can be skipped. The decision step includes a zero-upfront-cost policy so low funds cannot block signing.

All prompts are in `Assets/Resources/Localization/CityLanguages.txt`. The UI uses SimsFont and the shared UI scale.

DayNightCycle forces daylight during onboarding and disables its toggle. Ending/skipping restores the player's saved cycle preference without overwriting it.

Integration points outside this folder: GameClock pause predicate, CityUI.BuildingInspected, EconomyDetailsUI.PanelOpened, scheduler/maintenance/newspaper/news guards, SettingsUI restart button. No scene-specific objects must be wired by hand.
