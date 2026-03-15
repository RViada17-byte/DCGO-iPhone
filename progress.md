Original prompt: We need to have a deck selection screen before each World Duel, regardless of it being story or duelist board. This used to be implemented, we removed it to increase simplicity. how do we get it back?

- Investigated current World Duel launch flow and confirmed both `StoryPanel` and `DuelistBoardPanel` resolve enemy decks and load `BattleScene` immediately.
- Identified reusable `SelectBattleDeck` UI still exists and can be reinstated before scene load.
- Noted a stale `EnemyDeckData` risk: Story/Board assign it, but the offline bot-match path does not clear it before choosing a new opponent.
- Added a shared `SinglePlayerWorldDuelLauncher` that restores player deck selection before Story and Duelist Board battles while keeping enemy decks authored by content data.
- Updated Story and Duelist Board launch paths to delay session start and enemy deck assignment until the player confirms their own deck choice.
- Cleared stale `EnemyDeckData` when entering the legacy offline bot-match flow so it cannot inherit a previous World Duel opponent.
- Verified the change with `./tools/unity_check_compile.sh` using Unity `2022.3.62f3` and got a passing compile check.
