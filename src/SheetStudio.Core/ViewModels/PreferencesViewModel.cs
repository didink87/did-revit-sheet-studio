using System.Windows.Input;
using SheetStudio.Core.Create;
using SheetStudio.Core.Numbering;
using SheetStudio.Core.Preferences;
using SheetStudio.Core.Profiles;

namespace SheetStudio.Core.ViewModels;

public sealed class PreferencesViewModel : ObservableObject
{
    private readonly StudioViewModel _studio;
    private Prefs _p;

    internal PreferencesViewModel(StudioViewModel studio, Prefs current)
    {
        _studio = studio;
        _p = current;
        SaveProject = new RelayCommand(async () => await _studio.SavePrefsAsync(_p, userLevel: false));
        SaveUser = new RelayCommand(async () => await _studio.SavePrefsAsync(_p, userLevel: true));
        ResetToDefault = new RelayCommand(() => { _p = Prefs.Default; Raise(string.Empty); });
    }

    public Prefs Value => _p;
    public IReadOnlyList<string> ProfileIds => ProfileLoader.BuiltInIds;
    public IReadOnlyList<int> LangkahOptions { get; } = [1, 5, 10];

    public string ProfileId { get => _p.ProfileId; set => Update(_p with { ProfileId = value }); }
    public bool StrategyUrut { get => _p.Strategy == Strategy.Urut; set => Update(_p with { Strategy = value ? Strategy.Urut : Strategy.Rentang }); }
    public int Mulai { get => _p.Mulai; set => Update(_p with { Mulai = Math.Clamp(value, 0, 99) }); }
    public int Langkah { get => _p.Langkah; set => Update(_p with { Langkah = Math.Clamp(value, 1, 50) }); }
    public string TitleBlockA3 { get => _p.TitleBlockPerKertas[Kertas.A3]; set => SetTb(Kertas.A3, value); }
    public string TitleBlockA2 { get => _p.TitleBlockPerKertas[Kertas.A2]; set => SetTb(Kertas.A2, value); }
    public string TitleBlockA1 { get => _p.TitleBlockPerKertas[Kertas.A1]; set => SetTb(Kertas.A1, value); }
    public Kertas KertasDefault { get => _p.KertasDefault; set => Update(_p with { KertasDefault = value }); }
    public bool Thumbnail { get => _p.Thumbnail; set => Update(_p with { Thumbnail = value }); }
    public bool KonfirmasiTerapkan { get => _p.KonfirmasiTerapkan; set => Update(_p with { KonfirmasiTerapkan = value }); }
    public bool BatchLewatiGagal { get => _p.BatchLewatiGagal; set => Update(_p with { BatchLewatiGagal = value }); }

    public ICommand SaveProject { get; }
    public ICommand SaveUser { get; }
    public ICommand ResetToDefault { get; }

    private void SetTb(Kertas k, string value)
    {
        var d = _p.TitleBlockPerKertas.ToDictionary(kv => kv.Key, kv => kv.Value);
        d[k] = value;
        Update(_p with { TitleBlockPerKertas = d });
    }

    private void Update(Prefs next)
    {
        _p = next;
        Raise(string.Empty);
    }
}
