using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NatureCalls.UI
{
    /// <summary>A visual single-choice row; LobbyController owns the selected preview.</summary>
    public sealed class OutfitChoice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Text Label;
        public GameObject Check;
        public Image Swatch;
        public bool Selected { get; private set; }
        private bool hovered;
        private static readonly Color Ink = new(.95f, .93f, .85f);
        private static readonly Color SelectedInk = new(1, .84f, .48f);
        public void SetSelected(bool value) { Selected = value; Refresh(); }
        public void SetSwatch(Color color) { if (Swatch != null) Swatch.color = color; }
        public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
        private void OnDisable() { hovered = false; }
        private void Refresh()
        {
            Check.SetActive(Selected);
            Label.color = Selected ? SelectedInk : hovered ? Color.white : Ink;
        }
    }
}
