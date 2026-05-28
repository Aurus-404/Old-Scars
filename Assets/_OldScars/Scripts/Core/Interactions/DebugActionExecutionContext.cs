namespace OldScars.Core.Interactions
{
    public readonly struct DebugActionExecutionContext
    {
        public DebugActionExecutionContext(ActorInteractionContext actorContext, WorldObjectTags target, string equippedItemId)
        {
            ActorContext = actorContext;
            Target = target;
            EquippedItemId = equippedItemId;
        }

        public readonly ActorInteractionContext ActorContext;
        public readonly WorldObjectTags Target;
        public readonly string EquippedItemId;
    }
}
