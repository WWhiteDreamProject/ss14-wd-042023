using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Markings
{
    [Prototype("marking")]
    public sealed class MarkingPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; } = "uwu";

        public string Name { get; private set; } = default!;

        [DataField("bodyPart", required: true)]
        public HumanoidVisualLayers BodyPart { get; } = default!;

        [DataField("markingCategory", required: true)]
        public MarkingCategories MarkingCategory { get; } = default!;
        
        [DataField("speciesRestriction")]
        public List<string>? SpeciesRestrictions { get; }


        [DataField("sponsorOnly")]
        public bool SponsorOnly = false;

        [DataField("followSkinColor")]
        public bool FollowSkinColor { get; } = false;

        [DataField("forcedColoring")]
        public bool ForcedColoring { get; } = false;

        [DataField("coloring")]
        public MarkingColors Coloring { get; } = new();

        [DataField("sprites", required: true)]
        public List<SpriteSpecifier> Sprites { get; private set; } = default!;

        public Marking AsMarking()
        {
            return new Marking(ID, Sprites.Count);
        }
    }
}
