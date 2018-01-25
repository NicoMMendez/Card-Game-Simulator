﻿using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameSelectionMenu : SelectionPanel
{
    public const string SubmitInput = "Submit";
    public const string LoadInput = "Load";
    public const string SaveInput = "Save";
    public const string NewInput = "New";
    public const string FocusNameInput = "FocusName";
    public const string VerticalInput = "Vertical";
    public const string CancelInput = "Cancel";
    public const string GameLoadErrorMessage = "Failed to load game url! ";

    public RectTransform downloadPanel;
    public InputField urlInput;
    public Button cancelButton;
    public Button downloadButton;
    
    void Update()
    {
        if (downloadPanel.gameObject.activeSelf) {
            if (Input.GetButtonUp(SubmitInput) && downloadButton.interactable)
                StartDownload();
            else if (Input.GetButtonUp(SaveInput) && urlInput.interactable)
                Paste();
            else if (Input.GetButtonUp(NewInput) && urlInput.interactable)
                Clear();
            else if (Input.GetButtonUp(FocusNameInput) && urlInput.interactable)
                urlInput.ActivateInputField();
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown(CancelInput))
                HideDownloadPanel();
        } else {
            if (Input.GetButtonDown(SubmitInput) && EventSystem.current.currentSelectedGameObject == null)
                Hide();
            else if (Input.GetButtonUp(LoadInput))
                ShowDownloadPanel();
            else if (Input.GetButtonDown(VerticalInput) && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.SetSelectedGameObject(selectionContent.GetChild(0));
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown(CancelInput))
                Hide();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Rebuild(CardGameManager.Instance.AllCardGames.Keys.ToList(), SelectGame, CardGameManager.CurrentGameName);
    }

    public void SelectGame(bool isOn, string gameName)
    {
        if (isOn) {
            gameObject.SetActive(true);
            CardGameManager.Instance.SelectCardGame(gameName);
        }
        else
            Hide();
    }

    public void ShowDownloadPanel()
    {
        downloadPanel.gameObject.SetActive(true);
    }

    public void Paste()
    {
        if (urlInput.interactable)
            urlInput.text = UniClipboard.GetText();
    }

    public void Clear()
    {
        urlInput.text = string.Empty;
    }

    public void CheckDownloadUrl(string url)
    {
        downloadButton.interactable = System.Uri.IsWellFormedUriString(url.Trim(), System.UriKind.Absolute);
    }

    public void StartDownload()
    {
        CardGameManager.Instance.StartCoroutine(DownloadGame());
    }

    public IEnumerator DownloadGame()
    {
        CardGame newGame = new CardGame(Set.DefaultCode, urlInput.text.Trim()) {AutoUpdate = true};
        urlInput.text = string.Empty;
        urlInput.interactable = false;
        cancelButton.interactable = false;
        yield return newGame.Download();

        if (string.IsNullOrEmpty(newGame.Error)) {
            CardGameManager.Instance.AllCardGames [newGame.Name] = newGame;
            CardGameManager.Instance.SelectCardGame(newGame.Name);
        } else
            Debug.LogError(GameLoadErrorMessage + newGame.Error);

        urlInput.interactable = true;
        cancelButton.interactable = true;
        HideDownloadPanel();
    }

    public void HideDownloadPanel()
    {
        Show();
        downloadPanel.gameObject.SetActive(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
