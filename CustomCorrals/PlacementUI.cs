using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CustomCorrals
{
    class PlacementUI : MonoBehaviour
    {
        public PlacementHandler handler;
        RotationAnimator snappingImage;
        ScaleAnimator uiScaler;
        TMP_Text text;
        RectTransform textRect;
        void Awake()
        {
            uiScaler = gameObject.AddComponent<ScaleAnimator>();
            uiScaler.AnimationTime = 0.4f;
            var rect = GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.zero;
            rect.anchorMin = Vector2.one * -1;
            rect.anchorMax = Vector2.one * 2;
            var rightClip = new GameObject("rightClip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            rightClip.SetParent(rect, false);
            rightClip.sizeDelta = Vector2.zero;
            rightClip.anchorMin = new Vector2(0.5f, 0);
            rightClip.anchorMax = Vector2.one;
            snappingImage = new GameObject("snapImage", typeof(RectTransform), typeof(Image), typeof(RotationAnimator)).GetComponent<RotationAnimator>();
            snappingImage.AnimationTime = 0.4f;
            snappingImage.GetComponent<Image>().sprite = Main.snappingSrite;
            var snapRect = snappingImage.GetComponent<RectTransform>();
            snapRect.SetParent(rightClip, false);
            snapRect.sizeDelta = Vector2.zero;
            snapRect.anchorMin = new Vector2(-1,0);
            snapRect.anchorMax = Vector2.one;
            text = Instantiate(SRSingleton<HudUI>.Instance.currencyText, rect, false).GetComponent<TMP_Text>();
            text.fontSize /= 3;
            textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = Vector2.zero;
            textRect.anchorMin = new Vector2(0.25f, 0.5f);
            textRect.anchorMax = new Vector2(0.25f, 0.5f);
            text.lineSpacing = 0;
            text.autoSizeTextContainer = false;
        }
        void Update()
        {
            uiScaler.SetTarget((handler && handler.vacuum && handler.vacuum.InGadgetMode()) ? Vector3.one : Vector3.zero);
            snappingImage.SetTarget((handler && handler.snapping) ? 180 : 0);
            if (handler && handler.state == PlacementState.Started)
                text.text = "Cost: " + handler.Cost;
            else
                text.text = "";
            textRect.offsetMin = new Vector2(-text.preferredWidth - text.preferredHeight / 2, -text.preferredHeight / 2);
            textRect.offsetMax = new Vector2(0, text.preferredHeight / 2);
        }
    }
}