using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public delegate void OnSelectDelegate(Toggle toggle, string selection);

public class SelectionPanel : MonoBehaviour
{
    public RectTransform selectionContent;
    public RectTransform selectionTemplate;
    public ScrollRect scrollRect;
    public Button cancelButton;

    protected List<GameObject> Toggles { get; } = new List<GameObject>();

    public void Rebuild(SortedList<string, string> options, OnSelectDelegate changeKey, string currentKey = "")
    {
        if (options == null)
            options = new SortedList<string, string>();
        if (changeKey == null)
            changeKey = delegate { };
        if (currentKey == null)
            currentKey = string.Empty;

        Toggles.Clear();
        selectionContent.DestroyAllChildren();
        selectionContent.sizeDelta = new Vector2(selectionContent.sizeDelta.x, selectionTemplate.rect.height * options.Count);

        foreach (KeyValuePair<string, string> option in options)
        {
            Toggle toggle = Instantiate(selectionTemplate.gameObject, selectionContent).GetOrAddComponent<Toggle>();
            toggle.gameObject.SetActive(true);
            toggle.transform.localScale = Vector3.one;
            toggle.GetComponentInChildren<Text>().text = option.Value;
            toggle.interactable = true;
            toggle.isOn = option.Key.Equals(currentKey);
            toggle.onValueChanged.AddListener(isOn => changeKey(toggle, option.Key));
            Toggles.Add(toggle.gameObject);
        }

        selectionTemplate.gameObject.SetActive(options.Count < 1);
        selectionTemplate.GetComponent<Toggle>().isOn = options.Count > 0;

        float index = options.IndexOfKey(currentKey);
        if (index > 0 && index < options.Count && options.Count > 1)
            scrollRect.verticalNormalizedPosition = 1f - (index / (options.Count - 1f));
    }

    public void ScrollPage(float scrollDirection)
    {
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + (scrollDirection < 0 ? 0.1f : -0.1f));
    }

    public void SelectPrevious()
    {
        if (EventSystem.current.alreadySelecting || Toggles.Count < 1)
            return;

        if (!Toggles.Contains(EventSystem.current.currentSelectedGameObject))
        {
            EventSystem.current.SetSelectedGameObject(Toggles[0]);
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        for (int i = Toggles.Count - 1; i >= 0; i--)
        {
            if (!EventSystem.current.currentSelectedGameObject.Equals(Toggles[i]))
                continue;
            i--;
            if (i < 0)
                i = Toggles.Count - 1;
            EventSystem.current.SetSelectedGameObject(Toggles[i]);
            scrollRect.verticalNormalizedPosition = 1f - (i / (Toggles.Count - 1f));
            return;
        }
    }

    public void SelectNext()
    {
        if (EventSystem.current.alreadySelecting || Toggles.Count < 1)
            return;

        if (!Toggles.Contains(EventSystem.current.currentSelectedGameObject))
        {
            EventSystem.current.SetSelectedGameObject(Toggles[0]);
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        for (int i = 0; i < Toggles.Count; i++)
        {
            if (!EventSystem.current.currentSelectedGameObject.Equals(Toggles[i]))
                continue;
            i++;
            if (i == Toggles.Count)
                i = 0;
            EventSystem.current.SetSelectedGameObject(Toggles[i]);
            scrollRect.verticalNormalizedPosition = 1f - (i / (Toggles.Count - 1f));
            return;
        }
    }

}
