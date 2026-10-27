using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamePlay
{
    // Star rating popup; prefab is built by "GameHub/🎨 Build UI Panel Prefabs".
    // Tap star N to rate N, or press on the row and drag; the drag keeps tracking outside the row, clamped to 1..5.
    public class RatingPanel : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private Text questionText;
        [SerializeField] private Text ratingText;
        [SerializeField] private RectTransform starRow;
        [SerializeField] private Image[] stars;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Color starOnColor = Color.white;
        [SerializeField] private Color starOffColor = new Color(0.45f, 0.4f, 0.38f, 0.45f);

        /// <summary>Called with the rating 1..5, or 0 when skipped.</summary>
        public Action<int> OnDone;

        private int _rating;
        private bool _dragging;

        private void Awake()
        {
            submitButton.onClick.AddListener(() => OnDone?.Invoke(_rating));
            skipButton.onClick.AddListener(() => OnDone?.Invoke(0));
            SetRating(0);
        }

        public void Show(string missionId) => questionText.text = $"Bạn thấy {missionId} thế nào?";

        // Pointer events from children bubble up here, so only presses inside the star row start a rating.
        public void OnPointerDown(PointerEventData e)
        {
            _dragging = RectTransformUtility.RectangleContainsScreenPoint(starRow, e.position, e.pressEventCamera);
            if (_dragging) Track(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragging) Track(e);
        }

        private void Track(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(starRow, e.position, e.pressEventCamera, out var p);
            Rect r = starRow.rect;
            SetRating(Mathf.Clamp(Mathf.FloorToInt((p.x - r.xMin) / r.width * stars.Length) + 1, 1, stars.Length));
        }

        private void SetRating(int rating)
        {
            _rating = rating;
            for (int i = 0; i < stars.Length; i++)
                stars[i].color = i < rating ? starOnColor : starOffColor;
            ratingText.text = rating > 0 ? $"{rating}/5 sao" : "Chạm hoặc vuốt để chọn sao";
            submitButton.interactable = rating > 0;
        }
    }
}
