﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace CGS.Menus
{
    public class Popup : MonoBehaviour
    {
        public const string CloseLabel = "Close";
        public const string CancelLabel = "Cancel";

        public Text messageText;
        public Button yesButton;
        public Button noButton;
        public Button cancelButton;

        protected struct Message : IEquatable<Message>
        {
            public string Text;
            public UnityAction NoAction;
            public UnityAction YesAction;

            public bool Equals(Message other)
            {
                return !string.IsNullOrEmpty(Text) && Text.Equals(other.Text);
            }
        };

        protected Queue<Message> MessageQueue { get; } = new Queue<Message>();
        protected bool IsNewMessage { get; private set; }

        // Popup needs to update last to consume the input over what it covers
        void LateUpdate()
        {
            if (IsNewMessage)
            {
                IsNewMessage = false;
                return;
            }

            if (Input.GetKeyDown(Inputs.BluetoothReturn) || Input.GetButtonDown(Inputs.Submit))
            {
                if (yesButton.gameObject.activeInHierarchy)
                    yesButton.onClick?.Invoke();
                else
                    Close();
            }
            else if (Input.GetButtonDown(Inputs.Delete) && noButton.gameObject.activeInHierarchy)
                noButton.onClick?.Invoke();
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown(Inputs.Cancel))
                Close();
        }

        public void Show(string text)
        {
            Prompt(text, null);
        }

        public void Prompt(string text, UnityAction yesAction)
        {
            Ask(text, null, yesAction);
        }

        public void Ask(string text, UnityAction noAction, UnityAction yesAction)
        {
            Message message = new Message() { Text = text, NoAction = noAction, YesAction = yesAction };
            if (gameObject.activeSelf)
            {
                if (!MessageQueue.Contains(message))
                    MessageQueue.Enqueue(message);
                return;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            DisplayMessage(message);
        }

        private void DisplayMessage(Message message)
        {
            messageText.text = message.Text ?? string.Empty;
            yesButton.gameObject.SetActive(message.YesAction != null);
            noButton.gameObject.SetActive(message.NoAction != null);

            yesButton.onClick.RemoveAllListeners();
            if (message.YesAction != null)
                yesButton.onClick.AddListener(message.YesAction);
            yesButton.onClick.AddListener(Close);

            noButton.onClick.RemoveAllListeners();
            if (message.NoAction != null)
                noButton.onClick.AddListener(message.NoAction);
            noButton.onClick.AddListener(Close);

            if (message.YesAction == null && message.NoAction == null)
                cancelButton.GetComponentInChildren<Text>().text = CloseLabel;
            else
                cancelButton.GetComponentInChildren<Text>().text = CancelLabel;

            IsNewMessage = true;
        }

        public void Close()
        {
            if (MessageQueue.Count > 0)
                DisplayMessage(MessageQueue.Dequeue());
            else
                gameObject.SetActive(false);
        }
    }
}
