// An artifact from the rulebook. Lies face down on the board and in the equipment;
// it is turned face up (and works) only while it lies in a player's vault, for the owner of that vault.
public enum ArtifactType
{
    Minecart = 1,
    DivineEye,
    LostDocument,
    CursedStatue,
    HermesBoots,
    BottomlessPouch,
    Poltergeist,
    AtlasGloves,
    StickyFingers,
    PrometheanBond,
    EndingCondition,
    ExtraDie
}

public class Artifact
{
    public const int Count = 12;
    // points at the end of the game, like a coin
    public const int Points = 2;

    public readonly ArtifactType type;
    // everybody has seen it once it lay in a vault
    public bool revealed;
    // Poltergeist: put into a vault, the owner of that vault loses the next turn (waitingFor), after that every player
    // moves one field less (active) for as long as it stays in a vault
    public Player waitingFor;
    public bool active;

    public Artifact(ArtifactType type)
    {
        this.type = type;
    }

    public int Number => (int)type;
    public string Name => Names[Number - 1];
    public string Description => Descriptions[Number - 1];

    static readonly string[] Names =
    {
        "Wagonetka", "Boskie Oko", "Zaginiony dokument", "Przeklęta statuetka", "Buty Hermesa", "Bezdenna sakiewka",
        "Poltergeist", "Rękawice Atlasa", "Lepkie palce", "Prometejskie wiązanie", "Artefakt 11", "Artefakt 12"
    };

    static readonly string[] Descriptions =
    {
        "Zamiast rzutu kostką możesz przenieść się na pole sąsiadujące ze skarbcem innego gracza.",
        "Umożliwia podejrzenie zawartości dowolnego pola raz na turę.",
        "Twój skarbiec jest dla Ciebie jawny.",
        "Dopóki znajduje się w twoim skarbcu, musisz przenosić się o dokładnie tyle pól, ile wskazuje kostka (bez cofania się na pola, na których byłeś). Jeśli nie możesz przejść tylu pól, przechodzisz maksymalnie tyle, ile jesteś w stanie.",
        "Możesz poruszać się o jedno pole więcej, niż wskazuje kostka.",
        "Masz jedno miejsce więcej w ekwipunku.",
        "Po odłożeniu do skarbca tracisz następną turę. Po utracie tej tury wszyscy gracze poruszają się o maksymalnie jedno pole mniej, niż wskazuje kostka.",
        "Zamiast klasycznego przesuwania kafelkami możesz przesunąć dowolny kafelek w dowolne puste miejsce.",
        "Możesz zebrać jedną monetę przy przechodzeniu przez pole.",
        "Możesz zignorować jedno puste pole przy poruszaniu się (musisz skończyć na niepustym polu).",
        "Ignorujesz jeden z warunków koniecznych do skończenia gry (nie musisz mieć korony albo uzupełnionych wszystkich pól skarbca).",
        "Masz dodatkową kostkę przy rzucie za ruch. Wybierasz wyższy wynik."
    };
}
