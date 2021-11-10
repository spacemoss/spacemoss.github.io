using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Dialogue
{
    public string characterName;

    [TextArea(3, 30)]
    public string[] sentences;

    [TextArea(3, 30)]
    public string[] playerDialogue;
}
