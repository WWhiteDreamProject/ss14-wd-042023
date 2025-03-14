using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Managers;

public sealed class ChatSanitizationManager : IChatSanitizationManager
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IResourceManager _resources = default!;

    private Dictionary<string, string> _slangToNormal = new();

    private static readonly Dictionary<string, string> SmileyToEmote = new()
    {
        // I could've done this with regex, but felt it wasn't the right idea.
        { ":)", "chatsan-smiles" },
        { ":]", "chatsan-smiles" },
        { "=)", "chatsan-smiles" },
        { "=]", "chatsan-smiles" },
        { "(:", "chatsan-smiles" },
        { "[:", "chatsan-smiles" },
        { "(=", "chatsan-smiles" },
        { "[=", "chatsan-smiles" },
        { "^^", "chatsan-smiles" },
        { "^-^", "chatsan-smiles" },
        { ":(", "chatsan-frowns" },
        { ":[", "chatsan-frowns" },
        { "=(", "chatsan-frowns" },
        { "=[", "chatsan-frowns" },
        { "):", "chatsan-frowns" },
        { ")=", "chatsan-frowns" },
        { "]:", "chatsan-frowns" },
        { "]=", "chatsan-frowns" },
        { ":D", "chatsan-smiles-widely" },
        { "D:", "chatsan-frowns-deeply" },
        { ":O", "chatsan-surprised" },
        { ":3", "chatsan-smiles" }, //nope
        { ":S", "chatsan-uncertain" },
        { ":>", "chatsan-grins" },
        { ":<", "chatsan-pouts" },
        { "xD", "chatsan-laughs" },
        { "хд", "chatsan-laughs" },
        { ";-;", "chatsan-cries" },
        { ";_;", "chatsan-cries" },
        { "qwq", "chatsan-cries" },
        { ":u", "chatsan-smiles-smugly" },
        { ":v", "chatsan-smiles-smugly" },
        { ">:i", "chatsan-annoyed" },
        { ":i", "chatsan-sighs" },
        { ":|", "chatsan-sighs" },
        { ":p", "chatsan-stick-out-tongue" },
        { ":b", "chatsan-stick-out-tongue" },
        { "0-0", "chatsan-wide-eyed" },
        { "o-o", "chatsan-wide-eyed" },
        { "о-о", "chatsan-wide-eyed" }, // cyrillic о
        { "o.o", "chatsan-wide-eyed" },
        { "о.о", "chatsan-wide-eyed" }, // cyrillic о
        { "0_o", "chatsan-wide-eyed" },
        { "0_о", "chatsan-wide-eyed" }, // cyrillic о
        { "._.", "chatsan-surprised" },
        { ".-.", "chatsan-confused" },
        { "-_-", "chatsan-unimpressed" },
        { "o/", "chatsan-waves" },
        { "о/", "chatsan-waves" }, // cyrillic о
        { "^^/", "chatsan-waves" },
        { ":/", "chatsan-uncertain" },
        { ":\\", "chatsan-uncertain" },
        { "lmao", "chatsan-laughs" },
        { "lmao.", "chatsan-laughs" },
        { "lol", "chatsan-laughs" },
        { "lol.", "chatsan-laughs" },
        { "лол", "chatsan-laughs" },
        { "lel", "chatsan-laughs" },
        { "lel.", "chatsan-laughs" },
        { "kek", "chatsan-laughs" },
        { "kek.", "chatsan-laughs" },
        { "o7", "chatsan-salutes" },
        { "о7", "chatsan-salutes" }, // cyrillic о
        { ";_;7", "chatsan-tearfully-salutes"},
        { "idk", "chatsan-shrugs" }
    };

    private bool _doSanitize;

    public void Initialize()
    {
        _configurationManager.OnValueChanged(CCVars.ChatSanitizerEnabled, x => _doSanitize = x, true);

        try
        {
            var filterData = _resources.ContentFileReadAllText(new ResourcePath("/White/ChatFilters/slang.json"));
            _slangToNormal = JsonSerializer.Deserialize<Dictionary<string, string>>(filterData)!;
        }
        catch (Exception e)
        {
            Logger.ErrorS("chat", "Failed to load slang.json: {0}", e);
        }

    }

    public bool TrySanitizeOutSmilies(string input, EntityUid speaker, out string sanitized, [NotNullWhen(true)] out string? emote)
    {
        if (!_doSanitize)
        {
            sanitized = input;
            emote = null;
            return false;
        }

        input = input.TrimEnd();

        foreach (var (smiley, replacement) in SmileyToEmote)
        {
            if (input.EndsWith(smiley, true, CultureInfo.InvariantCulture))
            {
                sanitized = input.Remove(input.Length - smiley.Length).TrimEnd();
                emote = Loc.GetString(replacement, ("ent", speaker));
                return true;
            }
        }

        sanitized = input;
        emote = null;
        return false;
    }

    public string SanitizeOutSlang(string input)
    {
        string pattern = @"\b(?<word>\w+)\b";

        var newMessage = Regex.Replace(input, pattern ,
            match =>
            {
                return _slangToNormal.ContainsKey(match.Groups[1].Value.ToLower()) ? _slangToNormal[match.Groups[1].Value.ToLower()] : match.Value;
            }, RegexOptions.IgnoreCase);

        return newMessage;
    }

    public string SanitizeTags(string input)
    {
        return FormattedMessage.RemoveMarkup(input);
    }
}
