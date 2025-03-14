using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Content.Shared.White.TTS;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.Physics;

namespace Content.Shared.Preferences
{
    /// <summary>
    /// Character profile. Looks immutable, but uses non-immutable semantics internally for serialization/code sanity purposes.
    /// </summary>
    [DataDefinition]
    [Serializable, NetSerializable]
    public sealed class HumanoidCharacterProfile : ICharacterProfile
    {
        public const int MaxNameLength = 32;
        public const int MaxDescLength = 512;

        private readonly Dictionary<string, JobPriority> _jobPriorities;
        private readonly List<string> _antagPreferences;
        private readonly List<string> _traitPreferences;

        private HumanoidCharacterProfile(
            string name,
            string clownName,
            string mimeName,
            string borgName,
            string flavortext,
            string species,
            string voice,
            int age,
            Sex sex,
            Gender gender,
            string bodyType,
            HumanoidCharacterAppearance appearance,
            ClothingPreference clothing,
            BackpackPreference backpack,
            Dictionary<string, JobPriority> jobPriorities,
            PreferenceUnavailableMode preferenceUnavailable,
            List<string> antagPreferences,
            List<string> traitPreferences)
        {
            Name = name;
            ClownName = clownName;
            MimeName = mimeName;
            BorgName = borgName;
            FlavorText = flavortext;
            Species = species;
            Voice = voice;
            Age = age;
            Sex = sex;
            Gender = gender;
            BodyType = bodyType;
            Appearance = appearance;
            Clothing = clothing;
            Backpack = backpack;
            _jobPriorities = jobPriorities;
            PreferenceUnavailable = preferenceUnavailable;
            _antagPreferences = antagPreferences;
            _traitPreferences = traitPreferences;
        }

        /// <summary>Copy constructor but with overridable references (to prevent useless copies)</summary>
        private HumanoidCharacterProfile(
            HumanoidCharacterProfile other,
            Dictionary<string, JobPriority> jobPriorities,
            List<string> antagPreferences,
            List<string> traitPreferences)
            : this(other.Name, other.ClownName, other.MimeName, other.BorgName, other.FlavorText, other.Species, other.Voice, other.Age, other.Sex, other.Gender, other.BodyType, other.Appearance, other.Clothing, other.Backpack,
                jobPriorities, other.PreferenceUnavailable, antagPreferences, traitPreferences)
        {
        }

        /// <summary>Copy constructor</summary>
        private HumanoidCharacterProfile(HumanoidCharacterProfile other)
            : this(other, new Dictionary<string, JobPriority>(other.JobPriorities), new List<string>(other.AntagPreferences), new List<string>(other.TraitPreferences))
        {
        }

        public HumanoidCharacterProfile(
            string name,
            string clownName,
            string mimeName,
            string borgName,
            string flavortext,
            string species,
            string voice,
            int age,
            Sex sex,
            Gender gender,
            string bodyType,
            HumanoidCharacterAppearance appearance,
            ClothingPreference clothing,
            BackpackPreference backpack,
            IReadOnlyDictionary<string, JobPriority> jobPriorities,
            PreferenceUnavailableMode preferenceUnavailable,
            IReadOnlyList<string> antagPreferences,
            IReadOnlyList<string> traitPreferences)
            : this(name, clownName, mimeName, borgName, flavortext, species, voice, age, sex, gender, bodyType, appearance, clothing, backpack, new Dictionary<string, JobPriority>(jobPriorities),
                preferenceUnavailable, new List<string>(antagPreferences), new List<string>(traitPreferences))
        {
        }

        /// <summary>
        ///     Get the default humanoid character profile, using internal constant values.
        ///     Defaults to <see cref="SharedHumanoidAppearanceSystem.DefaultSpecies"/> for the species.
        /// </summary>
        /// <returns></returns>
        public HumanoidCharacterProfile() : this(
            "John Doe",
            "HONK",
            "Quiet",
            "Silicon",
            "",
            SharedHumanoidAppearanceSystem.DefaultSpecies,
            SharedHumanoidAppearanceSystem.DefaultVoice,
            18,
            Sex.Male,
            Gender.Male,
            SharedHumanoidAppearanceSystem.DefaultBodyType,
            new HumanoidCharacterAppearance(),
            ClothingPreference.Jumpsuit,
            BackpackPreference.Backpack,
            new Dictionary<string, JobPriority>
            {
                {SharedGameTicker.FallbackOverflowJob, JobPriority.High}
            },
            PreferenceUnavailableMode.SpawnAsOverflow,
            new List<string>(),
            new List<string>())
        {
        }

        /// <summary>
        ///     Return a default character profile, based on species.
        /// </summary>
        /// <param name="species">The species to use in this default profile. The default species is <see cref="SharedHumanoidAppearanceSystem.DefaultSpecies"/>.</param>
        /// <returns>Humanoid character profile with default settings.</returns>
        public static HumanoidCharacterProfile DefaultWithSpecies(string species = SharedHumanoidAppearanceSystem.DefaultSpecies)
        {
            return new(
                "John Doe",
                "HONK",
                "Quiet",
                "Silicon",
                "",
                species,
                SharedHumanoidAppearanceSystem.DefaultVoice,
                18,
                Sex.Male,
                Gender.Male,
                HumanoidCharacterAppearance.DefaultWithBodyType(species),
                HumanoidCharacterAppearance.DefaultWithSpecies(species),
                ClothingPreference.Jumpsuit,
                BackpackPreference.Backpack,
                new Dictionary<string, JobPriority>
                {
                    {SharedGameTicker.FallbackOverflowJob, JobPriority.High}
                },
                PreferenceUnavailableMode.SpawnAsOverflow,
                new List<string>(),
                new List<string>());
        }

        // TODO: This should eventually not be a visual change only.
        public static HumanoidCharacterProfile Random(HashSet<string>? ignoredSpecies = null)
        {
            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            var random = IoCManager.Resolve<IRobustRandom>();

            var species = random.Pick(prototypeManager
                .EnumeratePrototypes<SpeciesPrototype>()
                .Where(x => ignoredSpecies == null ? x.RoundStart : x.RoundStart && !ignoredSpecies.Contains(x.ID))
                .ToArray()
            ).ID;

            return RandomWithSpecies(species);
        }

        public static HumanoidCharacterProfile RandomWithSpecies(string species = SharedHumanoidAppearanceSystem.DefaultSpecies)
        {
            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            var random = IoCManager.Resolve<IRobustRandom>();

            var sex = Sex.Unsexed;
            var age = 18;
            var bodyType = SharedHumanoidAppearanceSystem.DefaultBodyType;
            if (prototypeManager.TryIndex<SpeciesPrototype>(species, out var speciesPrototype))
            {
                sex = random.Pick(speciesPrototype.Sexes);
                age = random.Next(speciesPrototype.MinAge, speciesPrototype.OldAge); // people don't look and keep making 119 year old characters with zero rp, cap it at middle aged
                bodyType = random.Pick(speciesPrototype.BodyTypes);
            }

            var voiceId = random.Pick(prototypeManager
                .EnumeratePrototypes<TTSVoicePrototype>()
                .Where(o => CanHaveVoice(o, sex)).ToArray()
            ).ID;

            var gender = sex == Sex.Male ? Gender.Male : Gender.Female;

            var name = GetName(species, gender);
            var clownName = GetClownName();
            var mimeName = GetMimeName();
            var borgName = GetBorgName();

            return new HumanoidCharacterProfile(name, clownName, mimeName, borgName, "", species, voiceId, age, sex, gender, bodyType, HumanoidCharacterAppearance.Random(species, sex), ClothingPreference.Jumpsuit, BackpackPreference.Backpack,
                new Dictionary<string, JobPriority>
                {
                    {SharedGameTicker.FallbackOverflowJob, JobPriority.High},
                }, PreferenceUnavailableMode.StayInLobby, new List<string>(), new List<string>());
        }

        public string Name { get; private set; }
        public string ClownName { get; private set; }
        public string MimeName { get; private set; }
        public string BorgName { get; private set; }
        public string FlavorText { get; private set; }
        public string Species { get; private set; }
        public string Voice { get; private set; }

        [DataField("age")]
        public int Age { get; private set; }

        [DataField("sex")]
        public Sex Sex { get; private set; }
        public string BodyType { get; private set; }

        [DataField("gender")]
        public Gender Gender { get; private set; }

        public ICharacterAppearance CharacterAppearance => Appearance;

        [DataField("appearance")]
        public HumanoidCharacterAppearance Appearance { get; private set; }
        public ClothingPreference Clothing { get; private set; }
        public BackpackPreference Backpack { get; private set; }
        public IReadOnlyDictionary<string, JobPriority> JobPriorities => _jobPriorities;
        public IReadOnlyList<string> AntagPreferences => _antagPreferences;
        public IReadOnlyList<string> TraitPreferences => _traitPreferences;
        public PreferenceUnavailableMode PreferenceUnavailable { get; private set; }

        public HumanoidCharacterProfile WithName(string name)
        {
            return new(this) { Name = name };
        }
        public HumanoidCharacterProfile WithClownName(string name)
        {
            return new(this) { ClownName = name };
        }
        public HumanoidCharacterProfile WithMimeName(string name)
        {
            return new(this) { MimeName = name };
        }
        public HumanoidCharacterProfile WithBorgName(string name)
        {
            return new(this) { BorgName = name };
        }

        public HumanoidCharacterProfile WithFlavorText(string flavorText)
        {
            return new(this) { FlavorText = flavorText };
        }

        public HumanoidCharacterProfile WithAge(int age)
        {
            return new(this) { Age = age };
        }

        public HumanoidCharacterProfile WithSex(Sex sex)
        {
            return new(this) { Sex = sex };
        }

        public HumanoidCharacterProfile WithBodyType(string bodyType)
        {
            return new(this) { BodyType = bodyType };
        }

        public HumanoidCharacterProfile WithGender(Gender gender)
        {
            return new(this) { Gender = gender };
        }

        public HumanoidCharacterProfile WithSpecies(string species)
        {
            return new(this) { Species = species };
        }

        public HumanoidCharacterProfile WithVoice(string voice)
        {
            return new(this) { Voice = voice };
        }

        public HumanoidCharacterProfile WithCharacterAppearance(HumanoidCharacterAppearance appearance)
        {
            return new(this) { Appearance = appearance };
        }

        public HumanoidCharacterProfile WithClothingPreference(ClothingPreference clothing)
        {
            return new(this) { Clothing = clothing };
        }
        public HumanoidCharacterProfile WithBackpackPreference(BackpackPreference backpack)
        {
            return new(this) { Backpack = backpack };
        }
        public HumanoidCharacterProfile WithJobPriorities(IEnumerable<KeyValuePair<string, JobPriority>> jobPriorities)
        {
            return new(this, new Dictionary<string, JobPriority>(jobPriorities), _antagPreferences, _traitPreferences);
        }

        public HumanoidCharacterProfile WithJobPriority(string jobId, JobPriority priority)
        {
            var dictionary = new Dictionary<string, JobPriority>(_jobPriorities);
            if (priority == JobPriority.Never)
            {
                dictionary.Remove(jobId);
            }
            else
            {
                dictionary[jobId] = priority;
            }
            return new(this, dictionary, _antagPreferences, _traitPreferences);
        }

        public HumanoidCharacterProfile WithPreferenceUnavailable(PreferenceUnavailableMode mode)
        {
            return new(this) { PreferenceUnavailable = mode };
        }

        public HumanoidCharacterProfile WithAntagPreferences(IEnumerable<string> antagPreferences)
        {
            return new(this, _jobPriorities, new List<string>(antagPreferences), _traitPreferences);
        }

        public HumanoidCharacterProfile WithAntagPreference(string antagId, bool pref)
        {
            var list = new List<string>(_antagPreferences);
            if(pref)
            {
                if(!list.Contains(antagId))
                {
                    list.Add(antagId);
                }
            }
            else
            {
                if(list.Contains(antagId))
                {
                    list.Remove(antagId);
                }
            }
            return new(this, _jobPriorities, list, _traitPreferences);
        }

        public HumanoidCharacterProfile WithTraitPreference(string traitId, bool pref)
        {
            var list = new List<string>(_traitPreferences);

            // TODO: Maybe just refactor this to HashSet? Same with _antagPreferences
            if(pref)
            {
                if(!list.Contains(traitId))
                {
                    list.Add(traitId);
                }
            }
            else
            {
                if(list.Contains(traitId))
                {
                    list.Remove(traitId);
                }
            }
            return new(this, _jobPriorities, _antagPreferences, list);
        }

        public string Summary =>
            Loc.GetString(
                "humanoid-character-profile-summary",
                ("name", Name),
                ("gender", Gender.ToString().ToLowerInvariant()),
                ("age", Age)
            );

        public bool MemberwiseEquals(ICharacterProfile maybeOther)
        {
            if (maybeOther is not HumanoidCharacterProfile other) return false;
            if (Name != other.Name) return false;
            if (ClownName != other.ClownName) return false;
            if (MimeName != other.MimeName) return false;
            if (BorgName != other.BorgName) return false;
            if (Age != other.Age) return false;
            if (Sex != other.Sex) return false;
            if (Gender != other.Gender) return false;
            if (BodyType != other.BodyType) return false;
            if (PreferenceUnavailable != other.PreferenceUnavailable) return false;
            if (Clothing != other.Clothing) return false;
            if (Backpack != other.Backpack) return false;
            if (!_jobPriorities.SequenceEqual(other._jobPriorities)) return false;
            if (!_antagPreferences.SequenceEqual(other._antagPreferences)) return false;
            if (!_traitPreferences.SequenceEqual(other._traitPreferences)) return false;
            return Appearance.MemberwiseEquals(other.Appearance);
        }

        public void EnsureValid(string[] sponsorMarkings)
        {
            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

            if (!prototypeManager.TryIndex<SpeciesPrototype>(Species, out var speciesPrototype))
            {
                Species = SharedHumanoidAppearanceSystem.DefaultSpecies;
                speciesPrototype = prototypeManager.Index<SpeciesPrototype>(Species);
            }

            var sex = Sex switch
            {
                Sex.Male => Sex.Male,
                Sex.Female => Sex.Female,
                Sex.Unsexed => Sex.Unsexed,
                _ => Sex.Male // Invalid enum values.
            };

            // ensure the species can be that sex and their age fits the founds
            var age = Age;
            if (speciesPrototype != null)
            {
                if (!speciesPrototype.Sexes.Contains(sex))
                {
                    sex = speciesPrototype.Sexes[0];
                }
                age = Math.Clamp(Age, speciesPrototype.MinAge, speciesPrototype.MaxAge);

                if (!prototypeManager.TryIndex<BodyTypePrototype>(BodyType, out var bodyType) ||
                    !SharedHumanoidAppearanceSystem.IsBodyTypeValid(bodyType, speciesPrototype, Sex))
                {
                    BodyType = prototypeManager.Index<BodyTypePrototype>(SharedHumanoidAppearanceSystem.DefaultBodyType).ID;
                }
            }

            var gender = Gender switch
            {
                Gender.Epicene => Gender.Epicene,
                Gender.Female => Gender.Female,
                Gender.Male => Gender.Male,
                Gender.Neuter => Gender.Neuter,
                _ => Gender.Epicene // Invalid enum values.
            };

            string name;
            string clownName;
            string mimeName;
            string borgName;
            if (string.IsNullOrEmpty(Name))
            {
                name = GetName(Species, gender);
            }
            else if (Name.Length > MaxNameLength)
            {
                name = Name[..MaxNameLength];
            }
            else
            {
                name = Name;
            }
            if (string.IsNullOrEmpty(ClownName))
            {
                clownName = GetClownName();
            }
            else if (ClownName.Length > MaxNameLength)
            {
                clownName = ClownName[..MaxNameLength];
            }
            else
            {
                clownName = ClownName;
            }
            if (string.IsNullOrEmpty(MimeName))
            {
                mimeName = GetMimeName();
            }
            else if (MimeName.Length > MaxNameLength)
            {
                mimeName = MimeName[..MaxNameLength];
            }
            else
            {
                mimeName = MimeName;
            }
            if (string.IsNullOrEmpty(BorgName))
            {
                borgName = GetBorgName();
            }
            else if (BorgName.Length > MaxNameLength)
            {
                borgName = BorgName[..MaxNameLength];
            }
            else
            {
                borgName = BorgName;
            }


            name = name.Trim();
            clownName = clownName.Trim();
            mimeName = mimeName.Trim();
            borgName = borgName.Trim();

            var configManager = IoCManager.Resolve<IConfigurationManager>();
            if (configManager.GetCVar(CCVars.RestrictedNames))
            {
                name = Regex.Replace(name, @"[^А-Я,а-я,0-9, -]", string.Empty);
                clownName = Regex.Replace(clownName, @"[^А-Я,а-я,0-9, -]", string.Empty);
                mimeName = Regex.Replace(mimeName, @"[^А-Я,а-я,0-9, -]", string.Empty);
                borgName = Regex.Replace(borgName, @"[^А-Я,а-я,0-9, -]", string.Empty);
            }

            if (configManager.GetCVar(CCVars.ICNameCase))
            {
                // This regex replaces the first character of the first and last words of the name with their uppercase version
                name = Regex.Replace(name,
                @"^(?<word>\w)|\b(?<word>\w)(?=\w*$)",
                m => m.Groups["word"].Value.ToUpper());
                clownName = Regex.Replace(clownName,
                    @"^(?<word>\w)|\b(?<word>\w)(?=\w*$)",
                    m => m.Groups["word"].Value.ToUpper());
                mimeName = Regex.Replace(mimeName,
                    @"^(?<word>\w)|\b(?<word>\w)(?=\w*$)",
                    m => m.Groups["word"].Value.ToUpper());
                borgName = Regex.Replace(borgName,
                    @"^(?<word>\w)|\b(?<word>\w)(?=\w*$)",
                    m => m.Groups["word"].Value.ToUpper());
            }

            if (string.IsNullOrEmpty(name))
            {
                name = GetName(Species, gender);
                clownName = GetClownName();
                mimeName = GetMimeName();
                borgName = GetBorgName();
            }

            string flavortext;
            if (FlavorText.Length > MaxDescLength)
            {
                flavortext = FormattedMessage.RemoveMarkup(FlavorText)[..MaxDescLength];
            }
            else
            {
                flavortext = FormattedMessage.RemoveMarkup(FlavorText);
            }

            var appearance = HumanoidCharacterAppearance.EnsureValid(Appearance, Species, BodyType, sponsorMarkings);

            var prefsUnavailableMode = PreferenceUnavailable switch
            {
                PreferenceUnavailableMode.StayInLobby => PreferenceUnavailableMode.StayInLobby,
                PreferenceUnavailableMode.SpawnAsOverflow => PreferenceUnavailableMode.SpawnAsOverflow,
                _ => PreferenceUnavailableMode.StayInLobby // Invalid enum values.
            };

            var clothing = Clothing switch
            {
                ClothingPreference.Jumpsuit => ClothingPreference.Jumpsuit,
                ClothingPreference.Jumpskirt => ClothingPreference.Jumpskirt,
                _ => ClothingPreference.Jumpsuit // Invalid enum values.
            };

            var backpack = Backpack switch
            {
                BackpackPreference.Backpack => BackpackPreference.Backpack,
                BackpackPreference.Satchel => BackpackPreference.Satchel,
                BackpackPreference.Duffelbag => BackpackPreference.Duffelbag,
                _ => BackpackPreference.Backpack // Invalid enum values.
            };

            var priorities = new Dictionary<string, JobPriority>(JobPriorities
                .Where(p => prototypeManager.HasIndex<JobPrototype>(p.Key) && p.Value switch
                {
                    JobPriority.Never => false, // Drop never since that's assumed default.
                    JobPriority.Low => true,
                    JobPriority.Medium => true,
                    JobPriority.High => true,
                    _ => false
                }));

            var antags = AntagPreferences
                .Where(prototypeManager.HasIndex<AntagPrototype>)
                .ToList();

            var traits = TraitPreferences
                         .Where(prototypeManager.HasIndex<TraitPrototype>)
                         .ToList();

            Name = name;
            ClownName = clownName;
            MimeName = mimeName;
            BorgName = borgName;
            FlavorText = flavortext;
            Age = age;
            Sex = sex;
            Gender = gender;
            Appearance = appearance;
            Clothing = clothing;
            Backpack = backpack;

            _jobPriorities.Clear();

            foreach (var (job, priority) in priorities)
            {
                _jobPriorities.Add(job, priority);
            }

            PreferenceUnavailable = prefsUnavailableMode;

            _antagPreferences.Clear();
            _antagPreferences.AddRange(antags);

            _traitPreferences.Clear();
            _traitPreferences.AddRange(traits);

            prototypeManager.TryIndex<TTSVoicePrototype>(Voice, out var voice);
            if (voice is null || !CanHaveVoice(voice, Sex))
                Voice = SharedHumanoidAppearanceSystem.DefaultSexVoice[sex];
        }

        public static bool CanHaveVoice(TTSVoicePrototype voice, Sex sex)
        {
            return voice.RoundStart && sex == Sex.Unsexed || (voice.Sex == sex || voice.Sex == Sex.Unsexed);
        }

        // sorry this is kind of weird and duplicated,
        /// working inside these non entity systems is a bit wack
        public static string GetName(string species, Gender gender)
        {
            var namingSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NamingSystem>();
            return namingSystem.GetName(species, gender);
        }

        public static string GetClownName()
        {
            var namingSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NamingSystem>();
            return namingSystem.GetClownName();
        }

        public static string GetMimeName()
        {
            var namingSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NamingSystem>();
            return namingSystem.GetMimeName();
        }

        public static string GetBorgName()
        {
            var namingSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NamingSystem>();
            return namingSystem.GetBorgName();
        }

        public override bool Equals(object? obj)
        {
            return obj is HumanoidCharacterProfile other && MemberwiseEquals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(
                    Name,
                    Species,
                    Age,
                    Sex,
                    Gender,
                    Appearance,
                    Clothing,
                    Backpack
                ),
                ClownName,
                MimeName,
                BorgName,
                PreferenceUnavailable,
                _jobPriorities,
                _antagPreferences,
                _traitPreferences
            );
        }
    }
}
