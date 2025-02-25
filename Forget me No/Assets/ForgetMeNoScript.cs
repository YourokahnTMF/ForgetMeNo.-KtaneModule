using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class ForgetMeNoScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public MeshRenderer[] LEDs;
    public TextMesh StageDisplay;
    public TextMesh RegularText;
    public TextMesh BigText;
    public static string[] IgnoredModules = null;

    private bool ModuleSolved = false;
    private bool ModuleInitialized = false;
    private bool LastStage = false;
    private bool StageRecovery = false;
    private bool Submission = false;
    private string DisplayNumberSequence = "";
    private string DisplayLEDSequence = "";
    private string CombinedConstantString = "";
    private string FinalAnswerString = "";
    private string FinalAnswerDisplay = "";
    private string InputDisplayString = "";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private int CurrentStageNumber = 1;
    private int EnteredStages = 0;
    private int OffsetEnteredStages = 0;
    private int RowOffset = 0;
    private int TotalStages;
    private int PreviousSolves;
    private Coroutine[] ButtonAnimCoroutines;

    Dictionary<string, string> Constants = new Dictionary<string, string>()
    {
        {"π"    ,"1415926535897932384626433832795028841971693993751058209749445923078164062862089986280348253421170679"},
        {"e"    ,"7182818284590452353602874713526624977572470936999595749669676277240766303535475945713821785251664274"},
        {"sqrt2","4142135623730950488016887242096980785696718753769480731766797379907324784621070388503875343276415727"},
        {"ln2"  ,"6931471805599453094172321214581765680755001343602552541206800094933936219696947156058633269964186875"},
        {"φ"    ,"6180339887498948482045868343656381177203091798057628621354486227052604628189024497072072041893911374"},
        {"γ"    ,"5772156649015328606065120900824024310421593359399235988057672348848628161332625388471532653213384543"},
        {"ρ"    ,"3247179572447460259609088544780973407344040569017333647511773196849943330457981735351419943227460373"},
        {"δ"    ,"6692016091029906718532038204662016172581855774757686327456513430046430596155893079608658315461735027"},
        {"λ"    ,"3035772690342963912570991121525518907307025046597086782995734358261341345935722034301986265121843828"},
        {"W(1)" ,"5671432904097840003294836545880241725504396327895736206961023466748606876187663252409701835347037716"}
    };
    List<string[]> AssignedConstants = new List<string[]>();

    //Not Interesting
    void Awake()
    {
        _moduleID = _moduleIdCounter++;

        ButtonAnimCoroutines = new Coroutine[Buttons.Length];

        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate
            {
                ButtonPress(x);
                return false;
            };
        }
        if (IgnoredModules == null)
            IgnoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Forget Me No.", new string[]{
                "14",
                "8",
                "Forget Enigma",
                "Forget Everything",
                "Forget It Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Forget Me No.",
                "Organization",
                "Purgatory",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "Übermodule",
                "Ültimate Custom Night",
                "The Very Annoying Button"
            });

        BigText.text = "";
        RegularText.text = "";
        StageDisplay.text = "";

    }
    // Use this for initialization
    void Start()
    {
        Module.OnActivate += delegate ()
        {
            ModuleInitialized = true;

            TotalStages = Bomb.GetSolvableModuleNames().Count(a => !IgnoredModules.Contains(a));

            for (int x = 0; x < TotalStages; x++)
            {
                DisplayNumberSequence += Rnd.Range(0, 10).ToString();
                DisplayLEDSequence += Rnd.Range(0, 10).ToString();
            }
            Debug.Log("NumSequence: " + DisplayNumberSequence);
            Debug.Log("LEDSequence: " + DisplayLEDSequence);

            DisplayStageInfo(0);
            AssignConstantsToButtons();
            GetConstantDigitString();
            StageCalculations();

            Logging($"The assigned constants, from 0 to 9, are: {AssignedConstants[0][0]} , { AssignedConstants[1][0] } , { AssignedConstants[2][0] } , { AssignedConstants[3][0] } , { AssignedConstants[4][0] } , { AssignedConstants[5][0] } , { AssignedConstants[6][0] } , { AssignedConstants[7][0] } , { AssignedConstants[8][0]} , { AssignedConstants[9][0]}");
        };
    }
    // Update is called once per frame
    void Update()
    {
        int NewStageCount = Bomb.GetSolvedModuleNames().Count(a => !IgnoredModules.Contains(a)) + 1;
        if (CurrentStageNumber < NewStageCount)
        {
            CurrentStageNumber = NewStageCount;

            if (!LastStage)
            {
                DisplayStageInfo(0);
            }
            else
            {
                InitializeSubmission();
            }

            LastStage = (CurrentStageNumber == TotalStages);
        }
    }
    //Button Press
    void ButtonPress(int pos)
    {
        Debug.Log(pos);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[pos].transform);

        Buttons[pos].AddInteractionPunch();

        if (ButtonAnimCoroutines[pos] != null)
            StopCoroutine(ButtonAnimCoroutines[pos]);

        ButtonAnimCoroutines[pos] = StartCoroutine(ButtonAnim(pos));

        if (ModuleInitialized && (TotalStages == 0) && !ModuleSolved)
        {
            Solve();
        }

        if (!ModuleSolved && Submission)
        {
            if (pos == int.Parse(FinalAnswerString[EnteredStages].ToString()))
            {
                HandleCorrectInput();
            }
            else
            {
                HandleWrongInput();
            }
        }
    }
    //Button Animation
    private IEnumerator ButtonAnim(int pos, float duration = 0.075f, float start = 0.0175f, float end = 0.015f)
    {
        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, start, Buttons[pos].transform.localPosition.z);

        float timer = 0;

        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Mathf.Lerp(start, end, timer / duration), Buttons[pos].transform.localPosition.z);
        }

        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, end, Buttons[pos].transform.localPosition.z);

        timer = 0;

        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Mathf.Lerp(end, start, timer / duration), Buttons[pos].transform.localPosition.z);
        }

        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, start, Buttons[pos].transform.localPosition.z);
    }
    //Logging
    private void Logging(string Message)
    {
        Debug.Log($"[Forget Me No. #{_moduleID}]: " + Message);
    }
    //Solve
    private void Solve()
    {
        Module.HandlePass();
        ModuleSolved = true;
        Logging("Module Solved");
    }
    //Displaying Stage Info
    private void DisplayStageInfo(int Stage)
    {
        // Normal Stage Display
        if (Stage == 0 && TotalStages != 0)
        {
            BigText.text = DisplayNumberSequence[CurrentStageNumber - 1].ToString();
            StageDisplay.text = CurrentStageNumber.ToString().PadLeft(2, '0');
            if (CurrentStageNumber != 1)
            {
                LEDs[int.Parse(DisplayLEDSequence[CurrentStageNumber - 2].ToString())].material.color = Color.black;
            }
            LEDs[int.Parse(DisplayLEDSequence[CurrentStageNumber - 1].ToString())].material.color = Color.green;
        }
        //Stage Display Upon Strike
        else if (StageRecovery)
        {
            BigText.text = DisplayNumberSequence[Stage - 1].ToString();
            StageDisplay.text = Stage.ToString().PadLeft(2, '0');
            LEDs[int.Parse(DisplayLEDSequence[Stage - 1].ToString())].material.color = Color.green;
        }
    }
    //When all modules are solved
    private void InitializeSubmission()
    {
        LEDs[int.Parse(DisplayLEDSequence[CurrentStageNumber - 2].ToString())].material.color = Color.black;
        BigText.text = "";
        StageDisplay.text = "--";

        int StagesCounted = 0;
        int Off = 0;

        while (InputDisplayString.Length < TotalStages + Off && StagesCounted < TotalStages + Off)
        {
            StagesCounted++;
            InputDisplayString += "-";
            if (StagesCounted % 12 == 0)
            {
                InputDisplayString += "\n";
                Off++;
            }
            else if (StagesCounted % 3 == 0)
            {
                InputDisplayString += " ";
                Off++;
            }
            Submission = true;
        }
        if (InputDisplayString.Length < 32)
            RegularText.text = InputDisplayString;
        else
            RegularText.text = InputDisplayString.Substring(0,32);

        Debug.Log("FinalNumberSequence: " + FinalAnswerDisplay);
    }
    //Assigning Constants to Buttons
    private void AssignConstantsToButtons()
    {
        string SerialNumber = Bomb.GetSerialNumber();
        int[] SerialNumberValues = new int[6];
        List<int> InvalidValues = new List<int>() {0,1,2,3,4,5,6,7,8,9};

        for (int i = 0; i < SerialNumber.Length; i++)
        {
            if (Alphabet.Contains(SerialNumber[i].ToString()))
            {
                SerialNumberValues[i] = (Alphabet.IndexOf(SerialNumber[i].ToString()) + 1) % 10;
            }
            else
            {
                SerialNumberValues[i] = int.Parse(SerialNumber[i].ToString());
            }
        }

        for (int i = 0;i < SerialNumberValues.Length; i++)
        {
            while (!InvalidValues.Contains(SerialNumberValues[i]))
                SerialNumberValues[i] = (SerialNumberValues[i] + 1) % 10;

            InvalidValues.Remove(SerialNumberValues[i]);

            AssignedConstants.Add(new string[] 
            { 
                Constants.Keys.ToArray()[SerialNumberValues[i]], Constants[Constants.Keys.ToArray()[SerialNumberValues[i]]] 
            });
        }

        string[] A = new string[] { Constants.Keys.ToArray()[InvalidValues[0]], Constants[Constants.Keys.ToArray()[InvalidValues[0]]] };
        string[] B = new string[] { Constants.Keys.ToArray()[InvalidValues[1]], Constants[Constants.Keys.ToArray()[InvalidValues[1]]] };
        string[] C = new string[] { Constants.Keys.ToArray()[InvalidValues[2]], Constants[Constants.Keys.ToArray()[InvalidValues[2]]] };
        string[] D = new string[] { Constants.Keys.ToArray()[InvalidValues[3]], Constants[Constants.Keys.ToArray()[InvalidValues[3]]] };

        if (Bomb.GetPortCount(Port.Parallel) != 0 && Bomb.GetOnIndicators().Contains("BOB"))
        {
            Assign(A, B, C, D);
        }
        else if (Bomb.GetBatteryCount() > 5)
        {
            Assign(D, C, B, A);
        }
        else if (Bomb.GetIndicators().Count() > 3)
        {
            Assign(A, D, C, B);
        }
        else if (Bomb.GetPortCount() == 0)
        {
            Assign(B, D, C, A);
        }
        else if (Bomb.GetBatteryCount() == 0)
        {
            Assign(C, B, D, A);
        }
        else if (Bomb.GetSerialNumberNumbers().Count() == 4)
        {
            Assign(B, A, D, C);
        }
        else if (Bomb.GetModuleIDs().Count() < 47 && Bomb.GetModuleIDs().Count() > 11)
        {
            Assign(D, A, B, C);
        }
        else if (CheckVowelInSN())
        {
            Assign(C, A, D, B);
        }
        else if (Bomb.GetPortPlates().Count(plt => plt.Count() == 0) > 0)
        {
            Assign(B, C, A, D);
        }
        else if (Bomb.GetPorts().Distinct().Count() == 6)
        {
            Assign(D, B, C, A);
        }
        else if (Bomb.GetOnIndicators().Count() > Bomb.GetOffIndicators().Count())
        {
            Assign(A, D, B, C);
        }
        else if (int.Parse(Bomb.GetSerialNumber()[5].ToString()) % 2 == 0)
        {
            Assign(C, B, A, D);
        }
        else if (int.Parse(Bomb.GetSerialNumber()[2].ToString()) % 2 == 1)
        {
            Assign(D, C, A, B);
        }
        else
        {
            Assign(B, A, C, D);
        }
    }
    //Helper Function
    private void Assign(string[] a, string[] b, string[] c, string[] d)
    {
        AssignedConstants.Add(a);
        AssignedConstants.Add(b);
        AssignedConstants.Add(c);
        AssignedConstants.Add(d);
    }
    //Check for a Vowel in the serial number
    private bool CheckVowelInSN()
    {
        bool Vowel = false;
        for (int i = 0; i < Bomb.GetSerialNumber().Length; i++)
        {
            if ("AEIOU".Contains(Bomb.GetSerialNumber()[i]))
                Vowel = true;
        }
        return Vowel;
    }
    //Getting the {Stage}'s Digit and concatonating them into a single string
    private void GetConstantDigitString()
    {
        int P;
        for (int i = 0;i < DisplayLEDSequence.Length; i++)
        {
            P = int.Parse(DisplayLEDSequence[i].ToString());
            CombinedConstantString += AssignedConstants[(P + 9) % 10][1][i % 100].ToString();
        }
    }
    //Calculating Stages
    private void StageCalculations()
    {
        int q = 0;
        int r;
        int s = 0;
        while (FinalAnswerString.Length != TotalStages)
        {
            r = (int.Parse(DisplayNumberSequence[q].ToString()) + int.Parse(CombinedConstantString[q].ToString())) % 10;
            FinalAnswerString += r.ToString();
            FinalAnswerDisplay += r.ToString();
            q++; s++;

            if (s % 12 == 0)
            {
                FinalAnswerDisplay += "\n";
            }
            else if (s % 3 == 0)
            {
                FinalAnswerDisplay += " ";
            }
        }
    }
    //Correct Input
    private void HandleCorrectInput()
    {
        EnteredStages++;
        OffsetEnteredStages++;

        StageRecovery = false;

        StageDisplay.text = "--";
        BigText.text = "";
        for (int i = 0; i < 10; i++)
            LEDs[i].material.color = Color.black;

        if (EnteredStages % 3 == 0)
            OffsetEnteredStages++;

        if (EnteredStages % 12 == 0 && EnteredStages != 12 && !(EnteredStages == TotalStages))
            RowOffset += 16;

        if (InputDisplayString.Length < 32)                       /* Only 2 Rows */
            RegularText.text = FinalAnswerDisplay.Substring(0        , OffsetEnteredStages            ) + InputDisplayString.Substring(OffsetEnteredStages,InputDisplayString.Length-OffsetEnteredStages);
        else if (InputDisplayString.Length - RowOffset - 16 < 16) /* Next Row is the last row */
            RegularText.text = FinalAnswerDisplay.Substring(RowOffset, OffsetEnteredStages - RowOffset) + InputDisplayString.Substring(OffsetEnteredStages);
        else                                                      /* Next Row */
            RegularText.text = FinalAnswerDisplay.Substring(RowOffset, OffsetEnteredStages - RowOffset) + InputDisplayString.Substring(OffsetEnteredStages,32 - OffsetEnteredStages + RowOffset);

        if (EnteredStages == TotalStages)
            Solve();
    }
    //Wrong Input and Stage Recovery
    private void HandleWrongInput()
    {
        Module.HandleStrike();
        StageRecovery = true;
        DisplayStageInfo(EnteredStages + 1);
        RegularText.text = "";
    }
}