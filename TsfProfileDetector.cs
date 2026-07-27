using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WeTypeCaps;

public readonly record struct TsfProfileIdentity(Guid Clsid, Guid ProfileGuid, string Source);

public readonly record struct ActiveProfile(
    int HResult,
    uint ProfileType,
    ushort LanguageId,
    Guid Clsid,
    Guid ProfileGuid,
    Guid Category,
    uint Flags)
{
    public bool IsMatch(TsfProfileIdentity identity) =>
        HResult == 0 &&
        ProfileType == 1 &&
        Clsid == identity.Clsid &&
        ProfileGuid == identity.ProfileGuid;
}

internal sealed class TsfProfileDetector : IDisposable
{
    private static readonly Guid ClsidTfInputProcessorProfiles =
        new("33C53A50-F456-4884-B049-85FD643ECFED");

    private static readonly Guid KeyboardCategory =
        new("34745C63-B2F0-4784-8B67-5E12C8701A31");

    private readonly TsfProfileIdentity _identity;
    private object? _comObject;
    private ITfInputProcessorProfileMgr? _manager;

    internal TsfProfileDetector(TsfProfileIdentity identity)
    {
        _identity = identity;
    }

    internal ActiveProfile GetActiveProfile()
    {
        EnsureManager();
        Guid category = KeyboardCategory;
        int hr = _manager!.GetActiveProfile(ref category, out TF_INPUTPROCESSORPROFILE profile);
        return new ActiveProfile(
            hr,
            profile.dwProfileType,
            profile.langid,
            profile.clsid,
            profile.guidProfile,
            profile.catid,
            profile.dwFlags);
    }

    internal bool IsWeTypeActive(out ActiveProfile profile)
    {
        try
        {
            profile = GetActiveProfile();
            return profile.IsMatch(_identity);
        }
        catch (Exception ex)
        {
            AppLog.Write($"TSF query failed: {ex.Message}");
            ReleaseManager();
            profile = default;
            return false;
        }
    }

    internal static TsfProfileIdentity ResolveWeTypeIdentity(AppConfig config)
    {
        TsfProfileIdentity? discovered = DiscoverFromRegistry();
        if (discovered is { } value)
        {
            return value;
        }

        return new TsfProfileIdentity(
            Guid.Parse(config.WeTypeTipClsid),
            Guid.Parse(config.WeTypeProfileGuid),
            "config");
    }

    private static TsfProfileIdentity? DiscoverFromRegistry()
    {
        foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using RegistryKey? tip = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\CTF\TIP");
                    if (tip is null)
                    {
                        continue;
                    }

                    foreach (string clsidText in tip.GetSubKeyNames())
                    {
                        if (!Guid.TryParse(clsidText, out Guid clsid))
                        {
                            continue;
                        }

                        using RegistryKey? profiles = tip.OpenSubKey(
                            $@"{clsidText}\LanguageProfile\0x00000804");
                        if (profiles is null)
                        {
                            continue;
                        }

                        foreach (string profileText in profiles.GetSubKeyNames())
                        {
                            using RegistryKey? profile = profiles.OpenSubKey(profileText);
                            string description = Convert.ToString(profile?.GetValue("Description")) ?? "";
                            string displayDescription =
                                Convert.ToString(profile?.GetValue("Display Description")) ?? "";
                            string iconFile = Convert.ToString(profile?.GetValue("IconFile")) ?? "";

                            bool looksLikeWeType =
                                description.Contains("WeType", StringComparison.OrdinalIgnoreCase) ||
                                displayDescription.Contains("WeType", StringComparison.OrdinalIgnoreCase) ||
                                iconFile.Contains("wetype", StringComparison.OrdinalIgnoreCase);

                            if (looksLikeWeType && Guid.TryParse(profileText, out Guid profileGuid))
                            {
                                return new TsfProfileIdentity(
                                    clsid,
                                    profileGuid,
                                    $"registry:{hive}/{view}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Write($"TIP discovery skipped {hive}/{view}: {ex.Message}");
                }
            }
        }

        return null;
    }

    private void EnsureManager()
    {
        if (_manager is not null)
        {
            return;
        }

        Type type = Type.GetTypeFromCLSID(ClsidTfInputProcessorProfiles, throwOnError: true)!;
        _comObject = Activator.CreateInstance(type)
            ?? throw new COMException("无法创建 TSF InputProcessorProfiles。");
        _manager = (ITfInputProcessorProfileMgr)_comObject;
    }

    private void ReleaseManager()
    {
        _manager = null;
        if (_comObject is not null && Marshal.IsComObject(_comObject))
        {
            Marshal.FinalReleaseComObject(_comObject);
        }

        _comObject = null;
    }

    public void Dispose()
    {
        ReleaseManager();
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TF_INPUTPROCESSORPROFILE
    {
        internal uint dwProfileType;
        internal ushort langid;
        internal Guid clsid;
        internal Guid guidProfile;
        internal Guid catid;
        internal nint hklSubstitute;
        internal uint dwCaps;
        internal nint hkl;
        internal uint dwFlags;
    }

    [ComImport]
    [Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfInputProcessorProfileMgr
    {
        [PreserveSig]
        int ActivateProfile(uint profileType, ushort langId, ref Guid clsid, ref Guid profile, nint hkl, uint flags);

        [PreserveSig]
        int DeactivateProfile(uint profileType, ushort langId, ref Guid clsid, ref Guid profile, nint hkl, uint flags);

        [PreserveSig]
        int GetProfile(uint profileType, ushort langId, ref Guid clsid, ref Guid profile, nint hkl, out TF_INPUTPROCESSORPROFILE result);

        [PreserveSig]
        int EnumProfiles(ushort langId, out nint profiles);

        [PreserveSig]
        int ReleaseInputProcessor(ref Guid clsid, uint flags);

        [PreserveSig]
        int RegisterProfile(
            ref Guid clsid,
            ushort langId,
            ref Guid profile,
            nint description,
            uint descriptionLength,
            nint iconFile,
            uint fileLength,
            uint iconIndex,
            nint substituteHkl,
            uint preferredLayout,
            [MarshalAs(UnmanagedType.Bool)] bool enabledByDefault,
            uint flags);

        [PreserveSig]
        int UnregisterProfile(ref Guid clsid, ushort langId, ref Guid profile, uint flags);

        [PreserveSig]
        int GetActiveProfile(ref Guid category, out TF_INPUTPROCESSORPROFILE profile);
    }
}

