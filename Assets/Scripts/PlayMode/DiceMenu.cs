﻿using UnityEngine;
using UnityEngine.UI;

public class DiceMenu : MonoBehaviour
{
    public const int DefaultMin = 1;
    public const int DefaultMax = 6;

    public GameObject diePrefab;
    public Text minText;
    public Text maxText;

    protected RectTransform Target { get; set; }

    private int _min;
    private int _max;

    void Start()
    {
        Min = DefaultMin;
        Max = DefaultMax;
    }

    void LateUpdate()
    {
        if (!Input.anyKeyDown || gameObject != CardGameManager.TopMenuCanvas?.gameObject)
            return;

        if (Input.GetKeyDown(Inputs.BluetoothReturn) || Input.GetButtonDown(Inputs.Submit))
            CreateAndHide();
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown(Inputs.Cancel))
            Hide();
    }

    public void Show(RectTransform playArea)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Target = playArea;
    }

    public void DecrementMin()
    {
        Min--;
    }

    public void IncrementMin()
    {
        Min++;
    }

    public void DecrementMax()
    {
        Max--;
    }

    public void IncrementMax()
    {
        Max++;
    }

    public void CreateAndHide()
    {
        Die die = Instantiate(diePrefab, Target).GetOrAddComponent<Die>();
        die.Min = Min;
        die.Max = Max;
        Hide();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public int Min {
        get { return _min; }
        set {
            _min = value;
            minText.text = _min.ToString();
        }
    }

    public int Max {
        get { return _max; }
        set {
            _max = value;
            maxText.text = _max.ToString();
        }
    }
}
