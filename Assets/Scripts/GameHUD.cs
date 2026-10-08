using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameHUD : MonoBehaviour
{
    // Scene references
    public RoomController room;
    public PlayerMovement player;
    public EnemyHealth boss;

    // Player health, stats and collected upgrades
    public Text healthText;
    public Text statsText;
    public Text buildText;
    private int displayedUpgradeCount = -1;

    // Dash cooldown
    public Text dashText;
    public Image dashFill;

    // Room title and progress
    public Text roomText;
    public Text objectiveText;

    // Boss health bar
    public GameObject bossPanel;
    public Image bossFill;

    // Upgrade selection
    public GameObject upgradePanel;
    public Button[] choiceButtons;
    public Text[] choiceTitles;
    public Text[] choiceDescriptions;
    public Text[] choiceNumbers;

    // Victory and defeat screen
    public GameObject resultPanel;
    public Text resultTitle;
    public Text resultBody;
    public Button restartButton;
    private Text resultButtonLabel;

    // Pause menu
    public GameObject pausePanel;
    public Button pauseRestartButton;
    public Button pauseMenuButton;
    public Button pauseQuitButton;

    // Tips and tutorial controls
    public GameObject noticePanel;
    public Text noticeText;
    public Button skipTutorialButton;
    private float noticeUntil;

    private void Start()
    {
        resultButtonLabel = restartButton.GetComponentInChildren<Text>(true);

        restartButton.onClick.AddListener(() =>
        {
            if (room.Phase == RoomPhase.Victory)
                room.ReturnToMainMenu();
            else
                room.RestartRun();
        });

        if (skipTutorialButton != null)
            skipTutorialButton.onClick.AddListener(room.SkipTutorial);

        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(room.RestartRun);

        if (pauseMenuButton != null)
            pauseMenuButton.onClick.AddListener(room.ReturnToMainMenu);

        if (pauseQuitButton != null)
            pauseQuitButton.onClick.AddListener(room.QuitGame);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;

            choiceButtons[i].onClick.AddListener(() => room.ChooseUpgrade(index));
        }

        roomText.text = room.IsTutorial ? "INTRODUKTION  ·  " + room.roomTitle.ToUpperInvariant()
            : "ETAGE " + RunState.CurrentFloor + " / " + RunState.TotalFloors + "  ·  " +
            (room.kind == RoomKind.Hallway ? "GANGEN"
            : (room.kind == RoomKind.Boss ? "BOSS · " : "") + room.roomTitle.ToUpperInvariant());
    }

    public void ShowNotice(string message, float duration = 4f)
    {
        if (noticeText == null)
            return;

        noticeText.text = message;
        noticeUntil = Time.time + duration;
    }

    public void ShowChoices(List<UpgradeDefinition> choices)
    {
        var heading = upgradePanel.transform.Find("Eyebrow");

        if (heading != null)
            heading.GetComponent<Text>().text = "V O G T E R E N   E R   F A L D E T";

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceButtons[i].gameObject.SetActive(i < choices.Count);

            if (i >= choices.Count)
                continue;

            choiceTitles[i].text = choices[i].title;
            choiceTitles[i].color = choices[i].color;
            choiceDescriptions[i].text = choices[i].description;
            choiceNumbers[i].color = choices[i].color;
        }
    }

    private void Update()
    {
        healthText.text = "LIV   " + RunState.Health + " / " + RunState.MaxHealth;
        healthText.color = RunState.Health <= 2 ? new Color(1, 0.45f, 0.35f) : new Color(0.6f, 1, 0.87f);

        dashFill.fillAmount = player.DashReady;
        dashText.text = player.DashReady >= 1f ? "DASH KLAR" : "DASH GENOPLADER";

        UpdateUpgradeSummary();

        statsText.text = "SKADE/SALVE " + RunState.Damage + "    SKUD/SEK " + (1f / RunState.FireInterval).ToString("0.0") + "    PROJEKTILER " + RunState.Projectiles;

        UpdatePanels();

        objectiveText.text = room.IsTutorial ? "" : "RUM RYDDET  " + RunState.CompletedCount + " / " + RunState.RoomsPerRun;
    }

    private void UpdateUpgradeSummary()
    {
        // Rebuild the list only after a reward. Repeated upgrades show a count.
        if (displayedUpgradeCount != RunState.Upgrades.Count)
        {
            displayedUpgradeCount = RunState.Upgrades.Count;

            var counts = new Dictionary<string, int>();
            var titles = new List<string>();

            foreach (var title in RunState.Upgrades)
            {
                if (!counts.ContainsKey(title))
                {
                    counts[title] = 0;
                    titles.Add(title);
                }

                counts[title]++;
            }

            for (int i = 0; i < titles.Count; i++)
            {
                string title = titles[i];

                if (counts[title] > 1)
                    titles[i] = title + " ×" + counts[title];
            }

            buildText.text = string.Join(" · ", titles);
        }
    }

    private void UpdatePanels()
    {
        upgradePanel.SetActive(room.Phase == RoomPhase.Upgrade);

        bool finished = room.Phase == RoomPhase.Victory || room.Phase == RoomPhase.Defeat;

        if (!room.IsTutorial && room.CanMove && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            ShowContextTip();

        if (noticePanel != null)
            noticePanel.SetActive(Time.time < noticeUntil && !room.Paused && !finished && room.Phase != RoomPhase.Upgrade);

        if (skipTutorialButton != null)
            skipTutorialButton.gameObject.SetActive(room.IsTutorial && !room.Paused);

        resultPanel.SetActive(finished);
        pausePanel.SetActive(room.Paused);
        bossPanel.SetActive(boss != null && room.Phase == RoomPhase.Fighting);

        if (boss != null)
            bossFill.fillAmount = (float)boss.Current / boss.maxHealth;

        if (finished)
        {
            bool won = room.Phase == RoomPhase.Victory;

            if (resultButtonLabel != null)
                resultButtonLabel.text = won ? "TIL HOVEDMENU   [R]" : "PRØV IGEN   [R]";

            resultTitle.text = won ? "DU OVERLEVEDE" : "KATAKOMBERNE TOG DIG";
            resultTitle.color = won ? new Color(0.6f, 1, 0.87f) : new Color(1f, 0.5f, 0.38f);

            resultBody.text = won ? "Alle " + RunState.TotalFloors + " etager er ryddet. Vogterne er faldet.\nDu nåede bunden af katakomberne."
                : room.IsTutorial ? "Du faldt under introduktionen.\nPrøv igen — eller spring introduktionen over."
                : "Du faldt på etage " + RunState.CurrentFloor + " og ryddede " + RunState.TotalRoomsCleared + " kamre i alt.\nPrøv en ny vej gennem katakomberne.";
        }
    }

    public void ShowContextTip()
    {
        if (room.IsTutorial || !room.CanMove)
            return;

        ShowNotice(GetContextTip(), 6f);
    }

    private string GetContextTip()
    {
        if (room.kind == RoomKind.Hallway)
        {
            if (!RunState.BossUnlocked)
                return "VÆLG EN DØR\nRyd de fire kamre for at åbne bossdøren.";

            if (RunState.HasNextFloor)
                return "BOSSDØREN ER ÅBEN\nBesejr vogteren for at vælge en opgradering.";

            return "DEN SIDSTE VOGTER\nBesejr bossen for at gennemføre Catacombs.";
        }

        if (room.Phase == RoomPhase.Exit)
        {
            if (room.kind == RoomKind.Boss)
                return "TRAPPEN ER ÅBEN\nGå mod nord for at fortsætte til etage " + (RunState.CurrentFloor + 1) + ".";

            return "RUMMET ER RYDDET\nGå gennem døren mod nord. Kun bosser giver opgraderinger.";
        }

        if (room.kind == RoomKind.Boss)
            return GetBossTip();

        return "KAMP\nHold venstre museknap for at skyde. SPACE undviger; bevæg dig med WASD.";
    }

    private string GetBossTip()
    {
        if (RunState.CurrentFloor == 1)
            return "VOGTEREN\nUndvig de ladende kugler. Hold afstand, og skyd mellem angreb.";

        if (RunState.CurrentFloor >= 5)
            return "STRÅLER, STORMLØB OG PULS\nStråler ved 75% og 40% liv. Søg bag en grav under pulsens opladning.";

        if (RunState.CurrentFloor == 4)
            return "STRÅLE, STORMLØB OG PULS\nÉn stråle ved 50% liv. Gem dig bag en grav, når pulsen lades op.";

        if (RunState.CurrentFloor == 3)
            return "STORMLØB OG PULS\nGem dig bag en grav under pulsens opladning. Dash til siden for stormløb.";

        return "VOGTERENS STORMLØB\nDen røde pil varsler et stormløb. Dash til siden med SPACE.";
    }
}
