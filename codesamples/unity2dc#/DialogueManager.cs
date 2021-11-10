using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq;

public class DialogueManager : MonoBehaviour
{
    #region Singleton
    public static DialogueManager instance;
    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("More than one instance of DialogueManager found!");
            return;
        }

        instance = this;
    }
    #endregion

    public GameObject dialogueUI;

    public TextMeshProUGUI npcNameText;
    public TextMeshProUGUI npcDialogueText;
    public Transform playerDialoguePanel;
    public GameObject playerDialogueButtonRef;
    private GameObject playerDialogueButtonsInst;
    private Button[] playerDialogueButtons;

    public Animator animator;

    public Queue<string> sentences;

    NPC npcRef;
    //bool isTalking = false;
    int currentResponseTracker = 0;

    PlayerManager playerManager;

    private List<GameObject> npcBalloon = new List<GameObject>();
    private List<Transform> npcBalloonTarget = new List<Transform>();
    public Transform dialogueBalloonParent;
    public GameObject dialogueBalloonRef;

    private Transform npcInteractingTransform;

    string choiceTrigger = "none";
    int choiceDialogueStage = 0;
    int[] choiceDialogueBranch = { 0 };
    bool choiceEndDialogue;
    [HideInInspector]
    public int nextDialogueBranch;
    [HideInInspector]
    public int nextDialogueStage;

    private string loadSceneName;

    public int conversationModifier;

    // Start is called before the first frame update
    void Start()
    {
        sentences = new Queue<string>();
        playerManager = PlayerManager.instance;
        dialogueUI.SetActive(false);
    }

    public void OnPlayerDialogueChoice(int choice)
    {
        // TODO: Consider how the player's biases might also influence the dialogue choice options they are given.
        currentResponseTracker = choice;
        DisableAllPlayerDialogueButtons();
        PlayerChoiceTriggerParser(choice);
    }

    public void PlayerChoiceTriggerParser(int choice)
    {
        int bias = DetermineBiases(npcRef, npcInteractingTransform, playerManager.player);
        int indexForTrigger = choice - 1;
        int choiceModifierInt = 0;
        string choiceActionString = null;
        bool hasChoiceOptions = false;

        // If our choice is one that prompts the end of the dialogue.
        if (choiceEndDialogue)
        {
            // Let the NPCInteract script know that the incoming DialoguePlayerChoice is going to be to end the dialogue
            // so that it doesn't try to register all the details from a non-existent choice button.
            if(npcInteractingTransform.GetComponent<NPCInteract>() != null)
            {
                npcInteractingTransform.GetComponent<NPCInteract>().endDialogue = choiceEndDialogue;
            }
            // If we choose to just leave, then end the dialogue (1). But if we choose to review the conversation, load that (2).
            if (choice == 1)
            {
                EndDialogue();
            } else if (choice == 2)
            {
                EndDialogue();
                LoadConversationReview();
            }

            return;
        }

        if (npcRef.conversationStage == 0)
        {
            // If we're at the 0 point of the conversation stage, we want to just draw from the defaults.
            if (npcRef.playerChoices.Length != 0)
            {
                // If we do have player choices, then look at triggers for them.
                if (indexForTrigger < npcRef.playerChoices.Length)
                {
                    choiceTrigger = npcRef.playerChoices[indexForTrigger].trigger;
                    choiceActionString = npcRef.playerChoices[indexForTrigger].actionString;
                    choiceModifierInt = npcRef.playerChoices[indexForTrigger].actionInt;
                    choiceDialogueStage = npcRef.playerChoices[indexForTrigger].dialogueStage;
                    choiceDialogueBranch = npcRef.playerChoices[indexForTrigger].dialogueBranch;
                    choiceEndDialogue = npcRef.playerChoices[indexForTrigger].endDialogue;
                    hasChoiceOptions = true; // Make sure the later part knows we have options to process, we do it this way since we're splitting.
                    //Debug.Log("Trigger: " + choiceTrigger + " Modifier: " + choiceModifierInt);
                } else
                {
                    // If there aren't any options, then we'll tell the bool, and also say this is a choice that should lead to an end dialogue.
                    hasChoiceOptions = false;
                    choiceEndDialogue = true;
                }
            } else
            {
                // Same as above, just different point.
                hasChoiceOptions = false;
                choiceEndDialogue = true;
            }
        } else
        {
            bool skipQuestCheck = false;
            if (npcRef.npcHub != null)
            {
                if (npcRef.npcHub.actionTriggeredDialogue)
                {
                    // Do stuff here based on it being an action triggere dialogue.
                    skipQuestCheck = true;
                    int conStage = npcRef.npcHub.familiarityLevel;
                    for (int i = 0; i < npcRef.npcHub.dialogueTrunks[conStage].Length; i++)
                    {
                        if (npcRef.npcHub.dialogueTrunks[conStage][i].trunkTrigger == npcRef.npcHub.currentAction)
                        {

                            if (indexForTrigger < npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias].Length)
                            {
                                choiceTrigger = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias][indexForTrigger].trigger;
                                choiceActionString = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias][indexForTrigger].actionString;
                                choiceModifierInt = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias][indexForTrigger].actionInt;
                                choiceDialogueStage = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias][indexForTrigger].dialogueStage;
                                choiceDialogueBranch = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias][indexForTrigger].dialogueBranch;
                                choiceEndDialogue = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias][indexForTrigger].endDialogue;
                                hasChoiceOptions = true;
                            }
                            else
                            {
                                hasChoiceOptions = false;
                                choiceEndDialogue = true;
                            }
                        }
                    }
                }
            }

            if(npcRef.quest != null && !skipQuestCheck)
            {
                // If we're not at the 0 point of the conversation stage, then we'll want to move on to quest stages.
                if (npcRef.quest.currentQuestStage != 0)
                {
                    int questStageIndex = npcRef.quest.currentQuestStage - 1;
                    int questBranchIndex = npcRef.quest.currentDialogueBranch;
                    // Make sure the index for the trigger, i.e. what the index number will be based on the choice's #, doesn't exceed the
                    // actual index of our choicetriggers in the array. This happens if we have buttons that do not cause triggers, but might
                    // still be used to move dialogue forward.
                    if (indexForTrigger < npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias].Length)
                    {
                        // We want to load these variables to set them below, but we won't set them for certain until QuestGiver determines the results
                        // based on its own checks and functions.
                        choiceTrigger = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias][indexForTrigger].trigger;
                        choiceActionString = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias][indexForTrigger].actionString;
                        choiceModifierInt = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias][indexForTrigger].actionInt;
                        choiceDialogueStage = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias][indexForTrigger].dialogueStage;
                        choiceDialogueBranch = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias][indexForTrigger].dialogueBranch;
                        choiceEndDialogue = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias][indexForTrigger].endDialogue;
                        hasChoiceOptions = true;
                        //Debug.Log("DM120: Quest Stage Index: " + questStageIndex + " indexForChoices: " + indexForTrigger + " Choice Dialogue Branch: "
                        //    + choiceDialogueBranch[0] + " choiceTrigger: " + choiceTrigger + " choiceActionString: " + choiceActionString + " questBranchIndex: " + questBranchIndex);
                        //Debug.Log("Trigger: " + choiceTrigger + " Modifier: " + choiceModifierInt);
                    }
                    else
                    {
                        hasChoiceOptions = false;
                        choiceEndDialogue = true;
                    }
                }
            }
        }

        // Only call the below checks if we have choice options, otherwise we don't want to throw null exceptions.
        if (hasChoiceOptions)
        {
            if (choiceTrigger != "none" && choiceModifierInt != 0)
            {
                StatsManager.instance.ModifyBias(npcInteractingTransform, npcInteractingTransform.GetComponent<CharacterStats>(), playerManager.player, "all", choiceModifierInt);
            }

            if (choiceTrigger == "acceptQuest" || choiceTrigger == "checkQuestProg")
            {
                npcInteractingTransform.GetComponent<Interactable>().DialoguePlayerChoice(choiceTrigger, choiceActionString);
            }
            else if (choiceTrigger == "loadScene") {
                if (choiceActionString != null)
                {
                    loadSceneName = choiceActionString;
                }
                else
                {
                    Debug.Log("Choice Action String needs to contain a Scene name.");
                }

            }
            else if (choiceTrigger == "checkStat")
            {
                if (choiceActionString == "charisma")
                {
                    int[] dice = { StatsManager.instance.GetDice(choiceActionString) };
                    StatsManager.instance.StatsCheck(playerManager.player.GetComponent<CharacterStats>().charisma, npcInteractingTransform.GetComponent<CharacterStats>().charisma, dice, 0, 0, out List<int> initResult, out List<int> targResult);
                    Debug.Log("Charisma check result: Player's result: " + initResult[0] + " NPC's result: " + targResult[0]);
                }

            } else if (choiceTrigger == "initializeNPCHub" || choiceTrigger == "passToNPCHub")
            {
                if(npcInteractingTransform.GetComponent<NPCHub>() != null)
                {
                    npcInteractingTransform.GetComponent<NPCHub>().DialoguePlayerChoice(choiceTrigger, choiceActionString, choiceModifierInt, choiceDialogueStage, choiceDialogueBranch, playerManager.player);

                }
            }
            else
            {
                if (choiceDialogueBranch.Length != 0)
                {
                    nextDialogueBranch = choiceDialogueBranch[0];
                }
                else
                {
                    nextDialogueBranch = 0;
                }
                nextDialogueStage = choiceDialogueStage;
            }
        }
        
        // Regardless of if we have options or not, we want to load our response to the choice.
        NPCResponce(currentResponseTracker);
    }

    public void ChooseNextDialogueBranch(int choicesIndex)
    {
        nextDialogueBranch = choiceDialogueBranch[choicesIndex];
        nextDialogueStage = choiceDialogueStage;
        //Debug.Log("DM 155: " + nextDialogueBranch);

    }

    public void SetNextQuestDialogueBranch()
    {
        // Right now we're only calling this from QuestGiver because QG determines quest aspects that decide / change what branches to follow.
        if (choiceDialogueBranch.Length != 1 && choiceDialogueBranch[0] != 0)
        {
            if (choiceTrigger != null || choiceTrigger != "none")
            {
                if (choiceTrigger != "checkQuestProg")
                {
                    //Debug.Log("DM 156: Current Dialogue Branch: " + npcRef.quest.currentDialogueBranch);
                    nextDialogueBranch = choiceDialogueBranch[0];
                    //Debug.Log("DM 158: Current Dialogue Branch: " + npcRef.quest.currentDialogueBranch);
                }
            }
        }
        else
        {
            //Debug.Log("Here at DM 118.");
            //Debug.Log("DM 165: Current Dialogue Branch: " + npcRef.quest.currentDialogueBranch + " Next Dialogue Branch: " + nextDialogueBranch);
            nextDialogueBranch = choiceDialogueBranch[0];
            //Debug.Log("DM 167: Current Dialogue Branch: " + npcRef.quest.currentDialogueBranch + " Next Dialogue Branch: " + nextDialogueBranch);
        }
    }

    public void NPCResponce(int RespondingTo)
    {
        if(RespondingTo > npcRef.dialogueNPC.Length - 1)
        {
            Debug.Log("No matching response found.");
            return;
        }
        StopAllCoroutines();
        StartCoroutine(TypeSentence(npcRef.dialogueNPC[RespondingTo]));
    }

    public void StartDialogue(NPC npc, Transform npcTransform)
    {
        // Dialogue with the player
        // UI Elements initializing and animating.
        dialogueUI.SetActive(true);
        animator.SetBool("isOpen", true);

        //NPC handling
        npcRef = npc;
        npcInteractingTransform = npcTransform;

        // UI setting up our UI elements.
        npcNameText.text = npcRef.npcName;
        //npcNameText.text = dialogue.characterName;

        //Dialogue / conversation status and biases
        //isTalking = true;
        currentResponseTracker = 0;
        int bias = DetermineBiases(npcRef, npcInteractingTransform, playerManager.player);

        // If this is the first stage of the conversation with no previous interactions. Load the defaults.
        if (npcRef.conversationStage == 0)
        {
            // Based on the bias, load the appropriate default variables.
            if (bias == 0)
            {
                npcRef.dialogueNPC = npcRef.defaultNpcDialoguePositive;
                npcRef.dialoguePlayer = npcRef.defaultPlayerDialoguePositive;
                npcRef.dialogueBalloon = npcRef.defaultNpcBalloonDialoguePos;
                npcRef.playerChoices = npcRef.defaultPlayerChoiceTriggersPos;
            }
            else if (bias == 1)
            {
                npcRef.dialogueNPC = npcRef.defaultNpcDialogueNeutral;
                npcRef.dialoguePlayer = npcRef.defaultPlayerDialogueNeutral;
                npcRef.dialogueBalloon = npcRef.defaultNpcBalloonDialogueNeut;
                npcRef.playerChoices = npcRef.defaultPlayerChoiceTriggersNeut;
            }
            else if (bias == 2)
            {
                npcRef.dialogueNPC = npcRef.defaultNpcDialogueNegative;
                npcRef.dialoguePlayer = npcRef.defaultPlayerDialogueNegative;
                npcRef.dialogueBalloon = npcRef.defaultNpcBalloonDialogueNeg;
                npcRef.playerChoices = npcRef.defaultPlayerChoiceTriggersNeg;
            }
        } else
        {
            bool skipQuestCheck = false;
            if(npcRef.npcHub != null)
            {
                if (npcRef.npcHub.actionTriggeredDialogue)
                {
                    // Do stuff here based on it being an action triggere dialogue.
                    skipQuestCheck = true;
                    int conStage = npcRef.npcHub.familiarityLevel;
                    for (int i = 0; i < npcRef.npcHub.dialogueTrunks[conStage].Length; i++)
                    {
                        if(npcRef.npcHub.dialogueTrunks[conStage][i].trunkTrigger == npcRef.npcHub.currentAction)
                        {
                            npcRef.dialogueNPC = npcRef.npcHub.dialogueTrunks[conStage][i].npcDialogue[bias];
                            npcRef.dialoguePlayer = npcRef.npcHub.dialogueTrunks[conStage][i].playerDialogue[bias];
                            npcRef.playerChoices = npcRef.npcHub.dialogueTrunks[conStage][i].choiceTriggers[bias];
                            npcRef.dialogueBalloon = npcRef.npcHub.dialogueTrunks[conStage][i].balloonDialogue[bias];
                        }
                    }
                }
            }
            //Otherwise, if our conversation stage has gone beyond the first instance and we are past it.
            // If we have a quest
            if(npcRef.quest != null && !skipQuestCheck)
            {
                // At the start, our currentQuestStage will be set to 1 by the Quest activation, but we need an index integer.
                int questStageIndex = npcRef.quest.currentQuestStage - 1;
                // We also want to get the current branch of dialogue we're using.
                int questBranchIndex = npcRef.quest.currentDialogueBranch;
                // Use the two above variables, along with bias, to determine which variables to load.
                npcRef.dialogueNPC = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questDialogueNPC[bias];
                npcRef.dialoguePlayer = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questDialoguePlayer[bias];
                npcRef.dialogueBalloon = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questBalloonDialogueNPC[bias];
                npcRef.playerChoices = npcRef.quest.questDialogueStages[questStageIndex][questBranchIndex].questChoiceTriggers[bias];
                //Debug.Log("DM 222: Quest Stage Index: " + questStageIndex + " questBranchIndex: " + questBranchIndex + " bias: " + bias);
            }
        }
        // We want to clear out any possible current dialoge buttons, so we first destory them, then load them witht he above variables.
        DestroyAllPlayerDialogueButtons();

        LoadPlayerDialogueButtons();

        // Clear the current queue of sentences.
        sentences.Clear();
        
        //dialogue.sentences
        foreach (string sentence in npcRef.dialogueNPC)
        {
            sentences.Enqueue(sentence);
        }

        // Go through each sentence we've queued.
        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {
        if(sentences.Count == 0)
        {
            OfferConclusionChoices();
            //EndDialogue();
            return;
        }

        string sentence = sentences.Dequeue();

        StopAllCoroutines();
        StartCoroutine(TypeSentence(sentence));
    }

    IEnumerator TypeSentence (string sentence)
    {
        npcDialogueText.text = "";
        foreach(char letter in sentence.ToCharArray())
        {
            npcDialogueText.text += letter;
            yield return null; // wait a single frame
        }

        if(loadSceneName != null)
        {
            yield return new WaitForSeconds(2);
            DialogueLoadScene();
        }

        //If our current choice is one that will end the dialogue
        if (choiceEndDialogue)
        {
            //Offer the player the choices that will conclude / end the dialogue.
            OfferConclusionChoices();
        }

        //If we have a quest.
        if(npcRef.quest != null)
        {
            // Check to see if our current dialogue branch is different from what our next branch will be
            //Debug.Log("DM 281: Current Dialogue Branch: " + npcRef.quest.currentDialogueBranch);
            if(npcRef.quest.currentDialogueBranch != nextDialogueBranch)
            {
                // If it's different, then set our current branch to the next one.
                npcRef.quest.currentDialogueBranch = nextDialogueBranch;
                npcRef.quest.currentQuestStage = nextDialogueStage + 1;
                if (!choiceEndDialogue)
                {
                    // If it isn't the end of the dialogue, then wait for 2 seconds and start
                    // a new dialogue with the next dialogue branch as the current branch set above.
                    yield return new WaitForSeconds(2);
                    StartDialogue(npcRef, npcInteractingTransform);
                }
            }
                
            //Debug.Log("DM 283: Current Dialogue Branch: " + npcRef.quest.currentDialogueBranch);
        }
            
    }

    public void DialogueLoadScene()
    {
        if (loadSceneName != null)
        {
            EndDialogue();
            DeleteDialogueBalloon(npcInteractingTransform, true);
            GameSystemManager.instance.LoadScene(loadSceneName, -1);
            loadSceneName = null;
        }
    }

    public void OfferConclusionChoices()
    {
        DestroyAllPlayerDialogueButtons();
        LoadPlayerDialogueButtons();
    }

    public void EndDialogue()
    {
        animator.SetBool("isOpen", false);
        DestroyAllPlayerDialogueButtons();
        dialogueUI.SetActive(false);
        if(npcInteractingTransform.GetComponent<NPCInteract>() != null)
        {
            npcInteractingTransform.GetComponent<NPCInteract>().interacting = false;
        }
        //isTalking = false;
        currentResponseTracker = 0;
        choiceEndDialogue = false;
        // If we have an active balloon, trigger delete it.
        if (npcBalloonTarget.Count != 0)
        {
            DeleteDialogueBalloon(npcInteractingTransform, false);
        }
    }

    public void DestroyAllPlayerDialogueButtons()
    {
        if (playerDialoguePanel.childCount != 0)
        {
            if(playerDialogueButtons != null)
            {
                for (int i = 0; i < playerDialogueButtons.Length; i++)
                {
                    // We have to detach all children first because we only Destroy on the next frame
                    // Which will be too slow for us to make changes to the player's choices.
                    playerDialoguePanel.DetachChildren();
                    Destroy(playerDialogueButtons[i].gameObject);
                    //playerDialogueButtons[i].GetComponent<DialoguePlayerButton>().DeleteDialogueButton();

                }
                playerDialogueButtons = null;
            }
        }
    }

    public void LoadPlayerDialogueButtons()
    {
        if (playerDialoguePanel.childCount == 0)
        {
            // If it's not the end of the dialogue, then we want to add choices based on our variables we set.
            if (!choiceEndDialogue)
            {
                for (int i = 0; i < npcRef.dialoguePlayer.Length; i++)
                {
                    playerDialogueButtonsInst = Instantiate(playerDialogueButtonRef, playerDialoguePanel) as GameObject;
                }
                // Load the dialogue buttons themselves.
                playerDialogueButtons = playerDialoguePanel.GetComponentsInChildren<Button>();
                for (int i = 0; i < playerDialogueButtons.Length; i++)
                {
                    TextMeshProUGUI text = playerDialogueButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                    text.text = npcRef.dialoguePlayer[i];
                    playerDialogueButtons[i].GetComponent<DialoguePlayerButton>().buttonResponseNumber = i + 1;
                    int responseNum = playerDialogueButtons[i].GetComponent<DialoguePlayerButton>().buttonResponseNumber;
                    playerDialogueButtons[i].onClick.AddListener(() => OnPlayerDialogueChoice(responseNum));
                }
                // Then load additional options for statCheck additions which show probability of the check.
                for(int i = 0; i < npcRef.playerChoices.Length; i++)
                {
                    string statToCheck = null;
                    // Determine if this is a direct stat check or if we need to take a few extra steps to access
                    // the stat we'll need to check
                    if (npcRef.playerChoices[i].trigger == "checkStat")
                    {
                        statToCheck = npcRef.playerChoices[i].actionString;
                    } else if (npcRef.playerChoices[i].trigger == "passToNPCHub")
                    {
                        // If we're passing it on to an NPC Hub we still want to do a stat check
                        // but we'll need to dig a bit deeper into the npcHub ActionChecks to find which stat
                        // we need to check for this specific action or dialogue choice.
                        if(npcRef.npcHub != null)
                        {
                            NPCHub hub = npcRef.npcHub;
                            if (hub.actionChecks.Length != 0)
                            {
                                for(int x = 0; x < hub.actionChecks.Length; x++)
                                {
                                    if (hub.actionChecks[x].action == npcRef.playerChoices[i].actionString)
                                    {
                                        statToCheck = hub.actionChecks[x].statCheck;
                                    }
                                }
                            }
                        }
                    }
                    // If we actually do have a statToCheck, then go ahead and initiate what we need to display it to the player.
                    if (statToCheck != null)
                    {
                        Transform statCheckPanelTransform = playerDialogueButtons[i].transform.Find("StatCheckPanel");
                        Image panel = statCheckPanelTransform.GetComponent<Image>();
                        statCheckPanelTransform.gameObject.SetActive(true);
                        TextMeshProUGUI statCheckText = statCheckPanelTransform.GetComponentInChildren<TextMeshProUGUI>();
                        int[] dice = { StatsManager.instance.GetDice(statToCheck) };
                        Stat playerStat;
                        playerManager.player.GetComponent<CharacterStats>().GetStatByString(statToCheck, out playerStat, out _, out _);
                        Stat npcStat;
                        npcInteractingTransform.GetComponent<CharacterStats>().GetStatByString(statToCheck, out npcStat, out _, out _);
                        int bias = StatsManager.instance.BiasCheck(npcInteractingTransform, playerManager.player);
                        float probability = StatsManager.instance.ProbabilityCheck(dice, false, false, playerStat.GetValue(), npcStat.GetValue());
                        if(bias < 5)
                        {
                            probability -= (float)bias;
                        } else if (bias > 5)
                        {
                            probability += (float)bias;
                        }
                        if(probability < 30.0)
                        { 
                            panel.color = Color.red;
                        } else if (probability > 30.0 && probability < 70.0)
                        {
                            panel.color = Color.yellow;
                        } else if (probability > 70.0)
                        {
                            panel.color = Color.green;
                        }
                        Debug.Log("playerStat: " + playerStat.GetValue() + " npcStat: " + npcStat.GetValue());
                        statCheckText.text = char.ToUpper(statToCheck.First()) + statToCheck.Substring(1).ToLower() + " : " + probability.ToString() + "% CoS.";
                    }
                }
            }
            else
            {
                // If it is the end of the dialogue, then we just want to create buttons to end the dialogue.
                for (int i = 0; i < 2; i++)
                {
                    playerDialogueButtonsInst = Instantiate(playerDialogueButtonRef, playerDialoguePanel) as GameObject;
                }

                playerDialogueButtons = playerDialoguePanel.GetComponentsInChildren<Button>();
                for (int i = 0; i < playerDialogueButtons.Length; i++)
                {
                    TextMeshProUGUI text = playerDialogueButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                    if(i == 0)
                    {
                        text.text = "Leave";
                    } else if(i == 1)
                    {
                        text.text = "Review Conversation";
                    }
                    playerDialogueButtons[i].GetComponent<DialoguePlayerButton>().buttonResponseNumber = i + 1;
                    int responseNum = playerDialogueButtons[i].GetComponent<DialoguePlayerButton>().buttonResponseNumber;
                    playerDialogueButtons[i].onClick.AddListener(() => OnPlayerDialogueChoice(responseNum));
                }
            }
            
        }
    }

    public void DisablePlayerDialogueButton(int choice)
    {
        if (playerDialoguePanel.childCount != 0)
        {
            for (int i = 0; i < playerDialogueButtons.Length; i++)
            {
                if(i == choice - 1)
                {
                    playerDialogueButtons[i].enabled = false;
                    playerDialogueButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = Color.grey;
                }
            }
        }
    }

    public void DisableAllPlayerDialogueButtons()
    {
        if (playerDialoguePanel.childCount != 0)
        {
            for (int i = 0; i < playerDialogueButtons.Length; i++)
            {
                playerDialogueButtons[i].enabled = false;
                playerDialogueButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = Color.grey;
            }
        }
    }

    public void TriggerDialogueBalloon(NPC npcForBalloon, Transform npcTransform, Transform interactingWith)
    {
        int bias = DetermineBiases(npcForBalloon, npcTransform, interactingWith.gameObject);

        if (npcBalloonTarget.Count != 0)
        {
            if (npcBalloonTarget.Contains(npcTransform))
            {
                for (int i = 0; i < npcBalloonTarget.Count; i++)
                {
                    if (npcBalloonTarget[i] == npcTransform)
                    {
                        // If there's already a balloon for this target transform active, then do nothing here.
                        return;
                    }
                }
            }
        }

        if (npcForBalloon.conversationStage == 0)
        {
            if (bias == 0)
            {
                npcForBalloon.dialogueBalloon = npcForBalloon.defaultNpcBalloonDialoguePos;
            }
            else if (bias == 1)
            {
                npcForBalloon.dialogueBalloon = npcForBalloon.defaultNpcBalloonDialogueNeut;
            }
            else if (bias == 2)
            {
                npcForBalloon.dialogueBalloon = npcForBalloon.defaultNpcBalloonDialogueNeg;
            }
        }
        else
        {
            bool skipQuestCheck = false;
            if (npcRef.npcHub != null)
            {
                if (npcRef.npcHub.actionTriggeredDialogue)
                {
                    // Do stuff here based on it being an action triggere dialogue.
                    skipQuestCheck = true;
                    int conStage = npcRef.npcHub.familiarityLevel;
                    for (int i = 0; i < npcRef.npcHub.dialogueTrunks[conStage].Length; i++)
                    {
                        if (npcRef.npcHub.dialogueTrunks[conStage][i].trunkTrigger == npcRef.npcHub.currentAction)
                        {
                            
                            npcForBalloon.dialogueBalloon = npcRef.npcHub.dialogueTrunks[conStage][i].balloonDialogue[bias];
                        }
                    }
                }
            }

            if (npcRef.quest != null && !skipQuestCheck)
            {
                int questStageIndex = npcForBalloon.quest.currentQuestStage - 1;
                int questBranchIndex = npcForBalloon.quest.currentDialogueBranch;
                npcForBalloon.dialogueBalloon = npcForBalloon.quest.questDialogueStages[questStageIndex][questBranchIndex].questBalloonDialogueNPC[bias];
            }
        }

        npcBalloonTarget.Add(npcTransform);
        npcBalloon.Add(Instantiate(dialogueBalloonRef, dialogueBalloonParent));
        npcBalloon[npcBalloon.Count - 1].GetComponent<DialogueBalloon>().CreateBalloon(npcTransform, npcForBalloon);
    }

    public void DeleteDialogueBalloon(Transform npcTransform, bool quickDelete)
    {
        if(npcBalloonTarget == null)
        {
            return;
        }
        if(npcBalloonTarget.Count != 0)
        {
            for (int i = 0; i < npcBalloonTarget.Count; i++)
            {
                if (npcBalloonTarget[i] == npcTransform)
                {
                    if (quickDelete)
                    {
                        if (npcBalloon[i] != null)
                        {
                            if (npcBalloon[i].GetComponent<DialogueBalloon>() != null)
                            {
                                npcBalloon[i].GetComponent<DialogueBalloon>().QuickDelete();
                            }
                        }
                    }
                    else
                    {
                        if(npcBalloon[i] != null)
                        {
                            if (npcBalloon[i].GetComponent<DialogueBalloon>() != null)
                            {
                                npcBalloon[i].GetComponent<DialogueBalloon>().DeleteBalloon(i);
                            }
                        }
                    }
                    
                }
            }
        }
    }

    public void RemoveBalloonEntries(int i)
    {
        if(npcBalloon.Count > i)
        {
            npcBalloon.RemoveAt(i);
        }
        
        if(npcBalloonTarget.Count > i)
        {
            npcBalloonTarget.RemoveAt(i);
        }
    }

    public void LoadConversationReview()
    {
        Debug.Log("You haven't implemented this yet.");
    }

    public int DetermineBiases(NPC npcBiasSource, Transform npcBiasedTransform, GameObject targetForBias)
    {
        conversationModifier = StatsManager.instance.BiasCheck(npcBiasedTransform, targetForBias);
        int loadDialogueMood = 0;
        // Determine if we have a 3 set of variables.
        if (CheckDialogueVariables(3, npcBiasSource) == null)
        {
            // If we don't have any dialogue, let us know.
            Debug.Log("No dialogue available.");
        }
        else if (CheckDialogueVariables(3, npcBiasSource).Count == 1)
        {
            // If we don't have all 3 dialogue options available, then just load whichever there is
            if (CheckDialogueVariables(3, npcBiasSource)[0] == "positive")
            {
                loadDialogueMood = 0;
            }
            else if (CheckDialogueVariables(3, npcBiasSource)[0] == "neutral")
            {
                loadDialogueMood = 1;
            }
            else if (CheckDialogueVariables(3, npcBiasSource)[0] == "negative")
            {
                loadDialogueMood = 2;
            }
        }
        else
        {
            // Otherwise, go through and see which one we should start with.
            if (conversationModifier > 5)
            {
                // Positive dialogue
                if (CheckDialogueVariables(0, npcBiasSource)[0] == "available")
                {
                    loadDialogueMood = 0;
                }
                else
                {
                    Debug.Log("No positive dialogue available.");
                }

            }
            else if (conversationModifier == 5)
            {
                // neutral conversation
                if (CheckDialogueVariables(1, npcBiasSource)[0] == "available")
                {
                    loadDialogueMood = 1;
                }
                else
                {
                    Debug.Log("No neutral dialogue available.");
                }

            }
            else if (conversationModifier < 5)
            {
                //negative conversation.
                if (CheckDialogueVariables(2, npcBiasSource)[0] == "available")
                {
                    loadDialogueMood = 2;
                }
                else
                {
                    Debug.Log("No negative dialogue available.");
                }

            }
        }

        return loadDialogueMood;

    }

    public List<string> CheckDialogueVariables(int moodToCheck, NPC npcToCheck)
    {
        List<string> dialoguesAvailable = new List<string>();
        int positiveLength = 0;
        int neutralLength = 0;
        int negativeLength = 0;

        if (npcToCheck.conversationStage == 0)
        {
            positiveLength = npcToCheck.defaultNpcDialoguePositive.Length;
            neutralLength = npcToCheck.defaultNpcDialogueNeutral.Length;
            negativeLength = npcToCheck.defaultNpcDialogueNegative.Length;
        }
        else if(npcToCheck.npcHub != null)
        {
            if (npcRef.npcHub.actionTriggeredDialogue)
            {
                // Do stuff here based on it being an action triggere dialogue.
                int conStage = npcRef.npcHub.familiarityLevel;
                for (int i = 0; i < npcRef.npcHub.dialogueTrunks[conStage].Length; i++)
                {
                    if (npcRef.npcHub.dialogueTrunks[conStage][i].trunkTrigger == npcRef.npcHub.currentAction)
                    {
                        positiveLength = npcToCheck.npcHub.dialogueTrunks[conStage][i].npcDialogue.Length;
                        neutralLength = npcToCheck.defaultNpcDialogueNeutral.Length;
                        negativeLength = npcToCheck.defaultNpcDialogueNegative.Length;
                    }
                }
            }
        }
        else if (npcToCheck.quest != null)
        {
            int tempStageIndex = npcToCheck.conversationStage - 1;
            int tempBranchIndex = npcToCheck.quest.currentDialogueBranch;
            positiveLength = npcToCheck.quest.questDialogueStages[tempStageIndex][tempBranchIndex].questDialogueNPC.Length;
            neutralLength = npcToCheck.defaultNpcDialogueNeutral.Length;
            negativeLength = npcToCheck.defaultNpcDialogueNegative.Length;
        }
        else
        {
            positiveLength = npcToCheck.defaultNpcDialoguePositive.Length;
            neutralLength = npcToCheck.defaultNpcDialogueNeutral.Length;
            negativeLength = npcToCheck.defaultNpcDialogueNegative.Length;
        }

        // moodToCheck key: 0 = positive, 1 = neutral, 2 = negative, 3 = all
        bool emptyVarSet = false;
        if (moodToCheck == 0 && positiveLength == 0)
        {
            emptyVarSet = true;
        }
        else if (moodToCheck == 1 && neutralLength == 0)
        {
            emptyVarSet = true;
        }
        else if (moodToCheck == 2 && negativeLength == 0)
        {
            emptyVarSet = true;
        }
        else if (moodToCheck == 3)
        {
            if (positiveLength == 0 && neutralLength == 0 && negativeLength == 0)
            {
                emptyVarSet = true;
            }
            else
            {
                if (positiveLength > 0)
                {
                    dialoguesAvailable.Add("positive");
                }
                if (neutralLength > 0)
                {
                    dialoguesAvailable.Add("neutral");
                }
                if (negativeLength > 0)
                {
                    dialoguesAvailable.Add("negative");
                }
                return dialoguesAvailable;
            }
        }

        if (emptyVarSet)
        {
            dialoguesAvailable = null;
        }
        else
        {
            dialoguesAvailable.Add("available");
        }

        return dialoguesAvailable;
    }
}
