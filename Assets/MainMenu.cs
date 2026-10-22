using System.Collections.Generic;
using TMPro;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main menu built from code, so it needs no setup in the scene.
// MapController shows it on start when no GameSettings were chosen yet.
public class MainMenu : MonoBehaviour
{
    static readonly string[] seatNames = UiFactory.SeatNames;
    static readonly Color[] seatColors = UiFactory.SeatColors;
    static readonly Color backgroundColor = UiFactory.Background;
    static readonly Color buttonColor = UiFactory.Button;
    static readonly Color selectedColor = UiFactory.Accent;
    static readonly Color infoColor = UiFactory.TextMuted;
    static readonly Color errorColor = new Color(1f, 0.45f, 0.45f, 1f);

    private MapController mapController;
    private GameMode mode = GameMode.Play;
    private int playerCount = 4;
    private PlayerType[] seatTypes = { PlayerType.Human, PlayerType.CpuAgent, PlayerType.CpuAgent, PlayerType.CpuAgent };

    private GameObject menuCanvas;
    private Button playModeButton;
    private Button trainingModeButton;
    private Button[] countButtons = new Button[3];
    private GameObject[] seatRows = new GameObject[4];
    private TextMeshProUGUI[] seatTypeLabels = new TextMeshProUGUI[4];
    private TextMeshProUGUI infoText;
    private GameObject speedRow;
    private Button[] speedButtons = new Button[GameSettings.SpeedNames.Length];
    // static, so the choice is kept when the menu is opened again
    private static int speedIndex = 1;

    public static void Show(MapController mapController)
    {
        MainMenu menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        menu.mapController = mapController;
        menu.Build();
        menu.Refresh();
    }

    // small button shown during the game that brings the menu back
    public static void ShowInGameMenuButton()
    {
        GameObject canvas = UiFactory.CreateCanvas("InGameMenuCanvas", 90);
        Button button = UiFactory.CreateButton(canvas.transform, "Menu", new Vector2(160, 60), ReturnToMenu);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20, -20);
    }

    public static void ReturnToMenu()
    {
        GameSettings.Current = null;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Build()
    {
        menuCanvas = UiFactory.CreateCanvas("MainMenuCanvas", 100);
        menuCanvas.transform.SetParent(transform, false);

        RectTransform background = UiFactory.CreateRect("Background", menuCanvas.transform);
        UiFactory.Stretch(background);
        background.gameObject.AddComponent<Image>().color = backgroundColor;

        RectTransform column = UiFactory.CreateRect("Column", background);
        column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(900, 1000);
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = UiFactory.CreateText(column, "ZŁODZIEJE", 84, selectedColor);
        title.fontStyle = FontStyles.Bold;

        UiFactory.CreateText(column, "Tryb", 36, Color.white);
        RectTransform modeRow = UiFactory.CreateRow(column, 80);
        playModeButton = UiFactory.CreateButton(modeRow, "Gra", new Vector2(300, 80), () => SetMode(GameMode.Play));
        trainingModeButton = UiFactory.CreateButton(modeRow, "Trening", new Vector2(300, 80), () => SetMode(GameMode.Training));

        UiFactory.CreateText(column, "Liczba graczy", 36, Color.white);
        RectTransform countRow = UiFactory.CreateRow(column, 80);
        for (int count = 2; count <= 4; count++)
        {
            int chosenCount = count;
            countButtons[count - 2] = UiFactory.CreateButton(countRow, count.ToString(), new Vector2(120, 80), () => SetPlayerCount(chosenCount));
        }

        for (int seat = 0; seat < seatNames.Length; seat++)
        {
            RectTransform row = UiFactory.CreateRow(column, 62);
            seatRows[seat] = row.gameObject;
            TextMeshProUGUI seatName = UiFactory.CreateText(row, seatNames[seat], 36, seatColors[seat]);
            seatName.gameObject.AddComponent<LayoutElement>().preferredWidth = 250;
            int chosenSeat = seat;
            Button typeButton = UiFactory.CreateButton(row, "", new Vector2(400, 62), () => CycleSeatType(chosenSeat));
            seatTypeLabels[seat] = typeButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        RectTransform speed = UiFactory.CreateRow(column, 64, 12);
        speedRow = speed.gameObject;
        TextMeshProUGUI speedLabel = UiFactory.CreateText(speed, "Tempo gry", 30, Color.white);
        UiFactory.SetSize(speedLabel, 170, 64);
        for (int i = 0; i < speedButtons.Length; i++)
        {
            int chosenSpeed = i;
            speedButtons[i] = UiFactory.CreateButton(speed, GameSettings.SpeedNames[i], new Vector2(150, 64), () => SetSpeed(chosenSpeed));
            speedButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
        }

        infoText = UiFactory.CreateText(column, "", 26, infoColor);
        infoText.enableWordWrapping = true;

        RectTransform actionRow = UiFactory.CreateRow(column, 90);
        Button startButton = UiFactory.CreateButton(actionRow, "Rozpocznij", new Vector2(360, 90), StartGame);
        SetSelected(startButton, true);
        UiFactory.CreateButton(actionRow, "Wyjdź", new Vector2(220, 90), Application.Quit);
    }

    void Refresh()
    {
        SetSelected(playModeButton, mode == GameMode.Play);
        SetSelected(trainingModeButton, mode == GameMode.Training);
        for (int i = 0; i < countButtons.Length; i++)
        {
            SetSelected(countButtons[i], playerCount == i + 2);
        }
        List<int> seats = GameSettings.SeatsFor(playerCount);
        for (int seat = 0; seat < seatRows.Length; seat++)
        {
            seatRows[seat].SetActive(seats.Contains(seat));
            seatTypeLabels[seat].text = TypeLabel(seatTypes[seat]);
        }
        // training runs as fast as mlagents-learn allows, without animations
        speedRow.SetActive(mode == GameMode.Play);
        for (int i = 0; i < speedButtons.Length; i++)
        {
            SetSelected(speedButtons[i], speedIndex == i);
        }

        infoText.color = infoColor;
        if (mode == GameMode.Play)
        {
            infoText.text = "Kliknij typ gracza, aby go zmienić. AI korzysta z wytrenowanych modeli, gracz losowy wykonuje przypadkowe ruchy.";
        }
        else if (Academy.Instance.IsCommunicatorOn)
        {
            infoText.text = "Połączono z mlagents-learn. Gracze AI będą się uczyć.";
        }
        else
        {
            infoText.text = "Brak połączenia z mlagents-learn. Uruchom \"mlagents-learn config.yaml --run-id=<nazwa>\" i dopiero wtedy włącz grę.";
        }
    }

    void SetMode(GameMode newMode)
    {
        mode = newMode;
        if (mode == GameMode.Training)
        {
            // nobody can click during training
            for (int seat = 0; seat < seatTypes.Length; seat++)
            {
                if (seatTypes[seat] == PlayerType.Human) seatTypes[seat] = PlayerType.CpuAgent;
            }
        }
        Refresh();
    }

    void SetSpeed(int index)
    {
        speedIndex = index;
        Refresh();
    }

    void SetPlayerCount(int count)
    {
        playerCount = count;
        Refresh();
    }

    void CycleSeatType(int seat)
    {
        switch (seatTypes[seat])
        {
            case PlayerType.Human:
                seatTypes[seat] = PlayerType.CpuAgent;
                break;
            case PlayerType.CpuAgent:
                seatTypes[seat] = PlayerType.CpuRandom;
                break;
            default:
                seatTypes[seat] = mode == GameMode.Training ? PlayerType.CpuAgent : PlayerType.Human;
                break;
        }
        Refresh();
    }

    void StartGame()
    {
        GameSettings settings = new GameSettings { mode = mode, speed = GameSettings.SpeedValues[speedIndex] };
        foreach (int seat in GameSettings.SeatsFor(playerCount))
        {
            settings.seats.Add(seat);
            settings.types.Add(seatTypes[seat]);
        }
        if (mode == GameMode.Training && !settings.types.Contains(PlayerType.CpuAgent))
        {
            infoText.color = errorColor;
            infoText.text = "Do treningu potrzebny jest co najmniej jeden gracz AI.";
            return;
        }

        GameSettings.Current = settings;
        Destroy(gameObject);
        mapController.StartGame(settings);
    }

    public static string TypeLabel(PlayerType type)
    {
        switch (type)
        {
            case PlayerType.Human: return "Człowiek";
            case PlayerType.CpuAgent: return "Komputer (AI)";
            default: return "Komputer (losowy)";
        }
    }

    static void SetSelected(Button button, bool selected)
    {
        button.image.color = selected ? selectedColor : buttonColor;
        button.GetComponentInChildren<TextMeshProUGUI>().color = selected ? UiFactory.AccentText : Color.white;
    }
}
