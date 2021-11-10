using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Cinemachine;

public class DialogueBalloon : MonoBehaviour
{
    [Header("Tweaks")]
    [SerializeField] public Transform lookAt;
    [SerializeField] public Vector3 offset;

    [Header("Logic")]
    private Camera mainCam;
    private string[] balloonDialogue;
    NPC myNPC;

    private TextMeshProUGUI dialogueText;

    bool hasSpoken = false;
    bool isSpeaking = false;

    private CinemachineBrain cineBrain;
    private CinemachineVirtualCamera virtCam;
    Vector2 defaultScale = new Vector2(1.0f, 1.0f);

    private void LateUpdate()
    {
        if(lookAt != null)
        {
            CreateBalloon(lookAt, myNPC);
        }
        
    }

    public void CreateBalloon(Transform target, NPC npc)
    {
        lookAt = target;
        myNPC = npc;
        balloonDialogue = myNPC.dialogueBalloon;
        mainCam = Camera.main;
        cineBrain = mainCam.GetComponent<CinemachineBrain>();
        virtCam = cineBrain.ActiveVirtualCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        dialogueText = this.GetComponentInChildren<TextMeshProUGUI>();
        if (lookAt != null && mainCam != null && balloonDialogue != null)
        {

            float reshapeSpeed = 5.0f;
            float minSize = 0.5f;
            float maxSize = 1.1f;
            // If the camera zooms in or out we want to do a inverse proportion so that the balloon scales up/down according to zoom level.
            if (virtCam.m_Lens.OrthographicSize != 5)
            {
                float orthoSize = virtCam.m_Lens.OrthographicSize / 5;
                float inverseProportionX = transform.localScale.x / orthoSize;
                float inverseProportionY = transform.localScale.y / orthoSize;
                float clampSizeX = Mathf.Clamp(inverseProportionX, minSize, maxSize);
                float clampSizeY = Mathf.Clamp(inverseProportionY, minSize, maxSize);
                float lerpSizeX = Mathf.Lerp(transform.localScale.x, clampSizeX, reshapeSpeed * Time.deltaTime);
                float lerpSizeY = Mathf.Lerp(transform.localScale.y, clampSizeY, reshapeSpeed * Time.deltaTime);
                Vector2 scaleSize = new Vector2(lerpSizeX, lerpSizeY);
                transform.localScale = scaleSize;
            }
            else
            {
                float lerpSizeX = Mathf.Lerp(transform.localScale.x, defaultScale.x, reshapeSpeed * Time.deltaTime);
                float lerpSizeY = Mathf.Lerp(transform.localScale.y, defaultScale.y, reshapeSpeed * Time.deltaTime);
                Vector2 scaleSize = new Vector2(lerpSizeX, lerpSizeY);
                transform.localScale = scaleSize;
            }
            
            Vector3 pos = mainCam.WorldToScreenPoint(lookAt.position + offset);
            if (!hasSpoken)
            {
                if(balloonDialogue.Length > 2)
                {
                    // If there's more than two entries in the balloonDialogue then we want to create a pause between typing.
                    StopAllCoroutines();
                    StartCoroutine(NaturalPause());
                }
                else {
                    // If there's only 2 entries or fewer, then we can assume it's a beginning and end.
                    StopAllCoroutines();
                    StartCoroutine(TypeSentence(balloonDialogue[0]));
                }
                hasSpoken = true;
            }

            

            if (transform.position != pos)
            {
                transform.position = pos;
            }
        }
        else
        {
            if(target == null)
            {
                //Debug.Log("No target Transform");
                Destroy(gameObject);
            }
            else if (mainCam == null)
            {
                Debug.Log("No camera set");
            }
            else if(balloonDialogue == null)
            {
                Debug.Log("No dialogue for balloon");
            }
        }
    }

    IEnumerator NaturalPause()
    {
        for (int i = 0; i < balloonDialogue.Length; i++)
        {
            if (i + 1 != balloonDialogue.Length)
            {
                if (!isSpeaking)
                {
                    StartCoroutine(TypeSentence(balloonDialogue[i]));
                }
                yield return new WaitForSeconds(balloonDialogue[i].Length / 3);
            }
        }
        
    }

    IEnumerator TypeSentence(string sentence)
    {
        dialogueText.text = "";
        foreach (char letter in sentence.ToCharArray())
        {
            isSpeaking = true;
            dialogueText.text += letter;
            yield return null; // wait a single frame
        }
        isSpeaking = false;
    }

    public void DeleteBalloon(int balloonReference)
    {
        
        StopAllCoroutines();
        // Check if we have more than one dialogue balloon option, if so, we're going to assume it's an ending one.
        if(balloonDialogue.Length > 1)
        {
            // Always get the last dialogue option as our ending one, in case we have more than one dialogue balloon options.
            StartCoroutine(TypeSentence(balloonDialogue[balloonDialogue.Length - 1]));
        }

        StartCoroutine(DestroyButton(balloonReference));
    }

    public void QuickDelete()
    {
        Destroy(gameObject);
    }

    IEnumerator DestroyButton(int balloonReference)
    {
        yield return new WaitForSeconds(2); // wait x secs
        DialogueManager.instance.RemoveBalloonEntries(balloonReference);
        Destroy(gameObject);
    }
}
