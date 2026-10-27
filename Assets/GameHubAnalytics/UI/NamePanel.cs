using System;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay
{
    // Name entry popup; prefab is built by "GameHub/🎨 Build UI Panel Prefabs".
    public class NamePanel : MonoBehaviour
    {
        [SerializeField] private InputField nameInput;
        [SerializeField] private Button confirmButton;

        /// <summary>Called with the trimmed, non-empty name.</summary>
        public Action<string> OnConfirm;

        private void Awake()
        {
            confirmButton.interactable = false;
            nameInput.onValueChanged.AddListener(v => confirmButton.interactable = v.Trim().Length > 0);
            confirmButton.onClick.AddListener(() => OnConfirm?.Invoke(nameInput.text.Trim()));
        }
    }
}
