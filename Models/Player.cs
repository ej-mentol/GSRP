using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace GSRP.Models;

public partial class Player : ObservableObject
{
    // --- Backing Fields for Observable Properties ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(Initials))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlias))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _alias = string.Empty;

    [ObservableProperty]
    private string _steamId2 = string.Empty;

    [ObservableProperty]
    private string _steamId64 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPersonaName))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private string _personaName = string.Empty;

    [ObservableProperty]
    private string _avatarHash = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegistrationDate))]
    [NotifyPropertyChangedFor(nameof(RegistrationDateString))]
    [NotifyPropertyChangedFor(nameof(AccountAge))]
    [NotifyPropertyChangedFor(nameof(DateColorBrush))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private uint _timeCreated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerColorBrush))]
    private Color? _playerColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PersonaNameColorBrush))]
    private Color? _personaNameColor;

    [ObservableProperty]
    private string _iconName = string.Empty;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private string _avatarPath = string.Empty;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private bool _isAvatarCached;

    [ObservableProperty]
    private bool _avatarDownloadFailed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileStatusText))]
    private ProfileStatus _profileStatus = ProfileStatus.Unknown;

    // --- Private fields for cached brushes ---
    private SolidColorBrush? _playerColorBrush;
    private SolidColorBrush? _personaNameColorBrush;
    private SolidColorBrush? _dateColorBrush;
    private int _previousAccountAge = -1;

    public Player()
    {
        GenerateFallbackBrush();
    }

    // --- Calculated & UI Properties ---

    public string DisplayName => !string.IsNullOrEmpty(Alias) ? $"{Name} ({Alias})" : Name;

    public string DetailText
    {
        get
        {
            var detail = SteamId2;
            if (!string.IsNullOrEmpty(PersonaName))
            {
                detail += $" ({PersonaName}";
                if (RegistrationDate != DateTime.MinValue) detail += $" | {RegistrationDateString}";
                detail += ")";
            }
            else if (RegistrationDate != DateTime.MinValue)
            {
                detail += $" ({RegistrationDateString})";
            }
            return detail;
        }
    }

    public DateTime RegistrationDate => TimeCreated == 0 ? DateTime.MinValue : DateTimeOffset.FromUnixTimeSeconds(TimeCreated).DateTime;

    public string RegistrationDateString => RegistrationDate == DateTime.MinValue ? "Unknown" : RegistrationDate.ToString("dd.MM.yyyy");

    public int AccountAge => RegistrationDate == DateTime.MinValue ? -1 : (DateTime.Now - RegistrationDate).Days;

    public string Initials
    {
        get
        {
            if (string.IsNullOrEmpty(Name)) return "??";
            if (Name.Length == 1) return Name.ToUpper();
            var parts = Name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return Name.Length >= 2 ? Name.Substring(0, 2).ToUpper() : Name.ToUpper();
        }
    }

    public bool HasAlias => !string.IsNullOrEmpty(Alias);
    public bool HasPersonaName => !string.IsNullOrEmpty(PersonaName);

    public string ProfileStatusText => ProfileStatus switch
    {
        ProfileStatus.Private => "Private Profile",
        ProfileStatus.NotFound => "Profile Not Found",
        _ => string.Empty
    };

    public SolidColorBrush? PlayerColorBrush
    {
        get
        {
            if (_playerColorBrush == null && PlayerColor.HasValue)
            {
                var brush = new SolidColorBrush(PlayerColor.Value);
                brush.Freeze();
                _playerColorBrush = brush;
            }
            return _playerColorBrush;
        }
    }

    public SolidColorBrush? PersonaNameColorBrush
    {
        get
        {
            if (_personaNameColorBrush == null && PersonaNameColor.HasValue)
            {
                var brush = new SolidColorBrush(PersonaNameColor.Value);
                brush.Freeze();
                _personaNameColorBrush = brush;
            }
            return _personaNameColorBrush;
        }
    }

    public SolidColorBrush DateColorBrush
    {
        get
        {
            var age = AccountAge;
            if (_dateColorBrush == null || _previousAccountAge != age)
            {
                Color color;
                if (age < 0 || age > 15) color = Color.FromRgb(166, 168, 171); // #a6a8ab
                else if (age <= 3) color = Colors.Red;
                else
                {
                    float factor = (float)age / 15;
                    color = Color.FromRgb(255, (byte)(factor * 127), 0);
                }
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                _dateColorBrush = brush;
                _previousAccountAge = age;
            }
            return _dateColorBrush;
        }
    }

    public SolidColorBrush FallbackAvatarBrush { get; private set; } = new SolidColorBrush(Colors.Gray);

    // --- Partial methods for invalidating cached properties ---

    partial void OnPlayerColorChanged(Color? value)
    {
        _playerColorBrush = null;
    }

    partial void OnPersonaNameColorChanged(Color? value)
    {
        _personaNameColorBrush = null;
    }

    partial void OnSteamId64Changed(string value)
    {
        GenerateFallbackBrush();
    }

    private void GenerateFallbackBrush()
    {
        if (string.IsNullOrEmpty(SteamId64) || !long.TryParse(SteamId64, out long id))
        {
            FallbackAvatarBrush = new SolidColorBrush(Colors.Gray);
            return;
        }
        var random = new Random((int)(id & 0xFFFFFFFF));
        var color = Color.FromRgb((byte)random.Next(50, 200), (byte)random.Next(50, 200), (byte)random.Next(50, 200));
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        FallbackAvatarBrush = brush;
        OnPropertyChanged(nameof(FallbackAvatarBrush));
    }

    public Player(Player other)
    {
        _name = other._name;
        _alias = other._alias;
        _steamId2 = other._steamId2;
        _steamId64 = other._steamId64;
        _personaName = other._personaName;
        _avatarHash = other._avatarHash;
        _timeCreated = other._timeCreated;
        _playerColor = other._playerColor;
        _iconName = other._iconName;
        _profileStatus = other._profileStatus;
        GenerateFallbackBrush();
    }

    public override string ToString() => $"{DisplayName} [{SteamId64}]";

    public override bool Equals(object? obj) => obj is Player other && SteamId64 == other.SteamId64;

    public override int GetHashCode() => SteamId64?.GetHashCode() ?? 0;
}