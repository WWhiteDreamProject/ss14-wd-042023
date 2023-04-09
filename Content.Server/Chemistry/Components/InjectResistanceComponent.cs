
namespace Content.Server.Chemistry.Components
{
    [RegisterComponent]
    public sealed class InjectResistanceComponent : Component
    {

        [DataField("injectResistance")]
        public bool NotInjectable = false;


    }
}
