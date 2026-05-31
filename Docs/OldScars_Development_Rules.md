# Old Scars — Development Rules for ChatGPT + Codex
Version: 0.2
Purpose: shared compact rule file for ChatGPT and Codex. This file stores specific technical/work rules that should not live in ChatGPT memory. Keep it concise, updated, and versioned in the repo.

Recommended repo path:
Docs/OldScars_Development_Rules.md

## 0. Source-of-truth policy

- ChatGPT memory stores high-level permanent workflows and project direction.
- This file stores specific, technical, small, or final rules.
- Repo docs store detailed history and milestone records.
- GitHub history is the real project history.

When a new specific rule is defined or changed:
1. ChatGPT should ask Mauro for the latest version of this file if it is not available.
2. ChatGPT should return an updated version of this file.
3. ChatGPT should also provide a compact prompt for Codex to apply the same change to the repo copy.
4. The ChatGPT copy and Codex/repo copy must stay equivalent.

## 1. General development rules

- Do not create systems just to create systems.
- Every new system must have a clear future use or be a bridge toward a future system.
- Debug/prototype tools should separate reusable foundation from temporary visual/debug presentation.
- Prefer small validated milestones over large unfinished systems.
- Plan before code.
- Do not rewrite validated systems unless there is a strong reason.
- Audit before deleting.
- Avoid premature final UI, final art, final animation, final VFX, or final audio.
- If a proposed change touches forbidden or high-risk systems, stop and explain before implementing.

## 2. Data-driven / JSON rules

- JSON defines data; C# executes closed logic.
- Do not turn JSON into free scripting.
- Definitions live in JSON/mods. Runtime instances live in save/runtime data.
- Do not save full objects; save definition IDs plus instance state.
- Data should be loaded once, validated, and accessed through GameDatabase.
- IDs must be stable, lowercase, readable, and unique.
- Prefer explicit IDs over implicit names.
- JSON should reference IDs, not scene objects.
- Loot tables are separate definitions; items should not declare their spawn sources.
- Effects must be closed/allowed C# effect types.
- `max_stack` is the source of simple item stacking in `ItemDefinition`.
- `max_stack = 1` means non-stackable.
- `max_stack > 1` allows simple merge in `ItemStorage`.
- `equippable` is a functional boolean in `ItemDefinition`, not a tag.
- Consumables use the closed `consumable.restore_needs` block.
- Consumable effects are data-driven parameters for closed C# logic, not free JSON scripting.

## 3. Naming rules

- Use clear English technical IDs.
- Prefer snake_case for data IDs.
- Avoid overly descriptive item IDs if state/condition belongs elsewhere.
- Examples:
  - Good: rusted_crowbar_01, scrap_metal_01, force_door, search_container.
  - Avoid: super_old_destroyed_oxidized_crowbar_that_opens_doors.
- Runtime/debug GameObject names may be human-readable, but IDs must remain stable.
- Display names are for UI/readability; IDs are for data references.

## 4. Tags and state rules

- Tags are system-facing context/state markers.
- Tags should enable decisions, consequences, actions, filtering, or interactions.
- Avoid adding tags that do not affect anything.
- Runtime tags represent current mutable state.
- Initial tags represent starting/default state.
- Visual systems may read tags but must not own gameplay logic.
- State-changing systems must be the ones that record state changes.
- Do not duplicate state-change reports from observers.

## 5. Interaction/action rules

- InteractionSystem must remain decoupled from UI, inventory, loot, pickup, and MonoBehaviour details.
- Action availability must come from ActionAvailabilityEvaluator and ActionAvailabilityResult.
- Diagnostic tools may expose evaluation results but must not duplicate availability logic.
- Menus should show executable/available actions only.
- Blocked actions may appear in diagnostics, not as executable actions.
- Contextual options should emerge from tags, stats, equipment, items, knowledge, state, distance, and context.
- Do not hardcode one-off object behavior when a generic rule/component is enough.

## 6. Feedback/debug tools rules

- GameplayFeedbackLog records structured gameplay facts that happened.
- Consuming an item should record structured feedback such as `ItemUsed`.
- ActionAvailabilityDiagnostics explains why actions are currently available or blocked.
- These systems must stay separate.
- Debug panels only display/read data; they do not decide gameplay.
- Debug panels should be hidden by default when they clutter the scene.
- Current hotkeys:
  - I = InventoryDebugPanel.
  - F7 = Gameplay Feedback Log.
  - F8 = Action Availability Diagnostics.
- A missing debug panel/log must not break gameplay.
- ActorNeedsDebugPanel and ItemStorageDebugPanel are debug tools, not final UI.
- ItemStorageDebugPanel should remain reusable for storages such as crates, corpses, backpacks, or traders.

## 7. Unity scene/component rules

- Scene roots holding gameplay components should not be disabled by visual helper components.
- Visual helper components may activate/deactivate child visual objects.
- Runtime visual state should reflect gameplay tags, not control them.
- Keep SampleScene changes minimal and purposeful.
- Avoid reorganizing the POI unless the milestone requires it.
- Use placeholders when validating behavior; do not chase final art.
- Prefer generic components over object-specific scripts.
- ItemStorage should remain the common base for inventories and containers.
- World containers may have internal storage before the content is accessible.
- Object state controls whether storage can be accessed; it does not determine whether storage exists.

## 8. WorldObjectStateView rules

- WorldObjectStateView reads WorldObjectTags runtime tags and applies visual rules.
- It must not modify tags.
- It must not decide gameplay.
- It must not change action availability.
- It must not replace InteractionSystem, ActionAvailabilityEvaluator, or feedback/diagnostics systems.
- It should use simple visual operations first:
  1. SetActive on child GameObjects.
  2. Local rotation on child Transforms.
- It should apply initial visual state on start/on enable.
- It may detect tag changes by polling/signature comparison for small debug POIs.
- It should not spam warnings every frame.
- Rules should be complete for the object: activate needed visuals and deactivate conflicting variants.

## 9. Documentation and Git rules

- After a milestone is validated, update:
  - Docs/Project_Roadmap.md
  - Docs/Current_Milestone.md
  - Docs/Development_Log.md
  - Docs/Next_Sprints.md
- After docs are updated, review changes in GitHub Desktop.
- Commit with a clear milestone message.
- Push to GitHub before starting the next milestone.
- Do not mark a milestone validated until it has been tested in Unity.

## 10. Codex prompt rules

- Prompts to Codex should be compact and not waste context/tokens.
- Include only the critical objective, restrictions, files/systems if necessary, and validation checklist.
- Use detailed prompts only for delicate/high-risk architecture.
- Prefer: objective + hard restrictions + expected output + validation.
- If Codex might touch forbidden systems, explicitly say to stop and explain first.

## 11. Things not to introduce prematurely

Do not introduce these unless explicitly approved for a milestone:
- Combat system.
- IA/NPC/faction systems.
- Save system.
- Final inventory UI.
- Final UI framework.
- Journal/quest log.
- EventBus/listeners/subscriptions/callbacks.
- Scripting in JSON.
- Visual profiles in JSON.
- Animation/VFX/audio systems.
- Large refactors of validated systems.
- Object-specific visual scripts when a generic component works.

## 12. Update protocol for this file

When updating this file:
- Keep it compact.
- Add rules only if they are likely to matter again.
- Avoid adding one-time implementation notes.
- Prefer short bullets.
- Update version only for meaningful structural changes.
- If a rule becomes obsolete, replace it instead of accumulating contradictions.
