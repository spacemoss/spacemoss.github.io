using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialoguePlayerButton : MonoBehaviour
{
    public int buttonResponseNumber;

    public void DeleteDialogueButton()
    {
        Destroy(this.gameObject);
    }
}
