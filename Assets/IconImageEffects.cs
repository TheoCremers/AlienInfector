using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IconImageEffects : MonoBehaviour
{
    private Button button;
    private Sprite baseSprite;
    public Image highlight;
    public Sprite altSprite;
    private bool alt = false;

    // Start is called before the first frame update
    void Start()
    {
        button = GetComponent<Button>();
        baseSprite = button.image.sprite;
    }
    
    public void SetAltSprite (bool useAlt) {
        button.image.sprite = useAlt? altSprite : baseSprite;
    }

    public void SetHighlight (bool active) {
        highlight.color = active? Color.white : Color.clear;
    }

    public void FlashHighlight (float duration) {

    }
}
