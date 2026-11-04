using SkyJump.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyJump.UI
{
    /// <summary>
    /// Feedback visual/sonoro para botões: cresce ao passar o mouse ou selecionar pelo teclado,
    /// "afunda" ao pressionar e toca um clique 8 bits.
    /// </summary>
    public class PixelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] RectTransform label;
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float pressOffset = 6f;

        bool hovered, selected, pressed;
        Vector2 labelBase;
        Vector3 baseScale;

        void Awake()
        {
            baseScale = transform.localScale;
            if (label != null) labelBase = label.anchoredPosition;
        }

        void OnDisable()
        {
            hovered = selected = pressed = false;
            transform.localScale = baseScale;
            if (label != null) label.anchoredPosition = labelBase;
        }

        void Update()
        {
            float target = (hovered || selected) ? hoverScale : 1f;
            if (pressed) target *= 0.97f;
            float s = Mathf.MoveTowards(transform.localScale.x / Mathf.Max(0.001f, baseScale.x), target, Time.unscaledDeltaTime * 1.5f);
            transform.localScale = baseScale * s;
            if (label != null) label.anchoredPosition = labelBase + (pressed ? Vector2.down * pressOffset : Vector2.zero);
        }

        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) => pressed = true;
        public void OnPointerUp(PointerEventData e) => pressed = false;
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerClick(PointerEventData e) => AudioManager.Play(Sfx.Click);
        public void OnSubmit(BaseEventData e) => AudioManager.Play(Sfx.Click);
    }
}
