using TMPro;
using UnityEngine;

namespace Landoria.HuginnCam
{
    // Manages the recording status label displayed below the minimap.
    internal sealed class RecordingStatusDisplay
    {
        private TextMeshProUGUI _label;

        // Displays the supplied recording status message.
        internal void Show(string message)
        {
            Attach(Minimap.instance);
            if (_label != null)
            {
                _label.text = message;
                _label.gameObject.SetActive(true);
            }
        }

        // Hides the recording status label without destroying it.
        internal void Hide()
        {
            if (_label != null)
            {
                _label.gameObject.SetActive(false);
            }
        }

        // Destroys the recording status label and releases its reference.
        internal void Dispose()
        {
            if (_label != null)
            {
                Object.Destroy(_label.gameObject);
                _label = null;
            }
        }

        // Creates and attaches the status label to the active minimap.
        private void Attach(Minimap minimap)
        {
            if (_label != null || minimap?.m_mapImageSmall == null || minimap.m_biomeNameSmall == null)
            {
                return;
            }

            GameObject target = new GameObject("HuginnCamRecordingStatus", typeof(RectTransform));
            target.SetActive(false);
            target.transform.SetParent(minimap.m_smallRoot.transform, false);
            ConfigurePosition(target.GetComponent<RectTransform>(), minimap.m_mapImageSmall.rectTransform);
            ConfigureLabel(target.AddComponent<TextMeshProUGUI>(), minimap.m_biomeNameSmall);
        }

        // Positions the status label immediately below the small map.
        private static void ConfigurePosition(RectTransform rect, RectTransform mapRect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(480f, 30f);
            rect.position = mapRect.TransformPoint(new Vector3(mapRect.rect.center.x, mapRect.rect.yMin - 32f, 0f));
            rect.SetAsLastSibling();
        }

        // Configures the status label to match Valheim's minimap typography.
        private void ConfigureLabel(TextMeshProUGUI label, TMP_Text reference)
        {
            _label = label;
            _label.font = reference.font;
            _label.fontSharedMaterial = reference.fontSharedMaterial;
            _label.fontSize = reference.fontSize;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.text = string.Empty;
        }
    }
}
