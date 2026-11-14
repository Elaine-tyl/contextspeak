namespace ContextSpeak.Life
{
    /// An information board at a viewing point: [E] View Animal Information.
    public sealed class AnimalInteraction : InteractableObject
    {
        public string SpeciesId = "elephant";
        public AnimalHabitat Habitat;
        public override bool CanInteract => base.CanInteract && LocationFlow.Current is ZooFlow flow && !flow.Busy;
        public override void Interact(InteractionManager manager) { if (LocationFlow.Current is ZooFlow flow) flow.ShowAnimal(SpeciesId, Habitat); }
    }
}
