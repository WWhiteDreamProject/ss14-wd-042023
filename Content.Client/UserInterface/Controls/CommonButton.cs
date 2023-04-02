using System.Diagnostics.CodeAnalysis;
using Content.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.IoC.Exceptions;
using TerraFX.Interop.Windows;

namespace Content.Client.UserInterface.Controls;

/// <summary>
///     Just a <see cref="Robust.Client.UserInterface.Controls.Button"/> but with sound.
/// </summary>
[Virtual]
public class CommonButton : Button
{

    public CommonButton()
    {
        IoCManager.InjectDependencies(this);
    }
}
