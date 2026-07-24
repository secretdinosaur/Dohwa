using UnityEngine;
using UnityEngine.Localization;
using System;
using UnityEngine.UI;
[CreateAssetMenu(menuName = "Tutorial")]
public class Tutorial : ScriptableObject
{
    public string TutorialName;
    public LocalizedString Localize_Title;
    public string tutorialTitle => Localize_Title.GetLocalizedString();

    public TutorialPage[] Pages;
}


[System.Serializable]
public class TutorialPage
{
    public Sprite Image;

    public LocalizedString Localize_Text;
    public string Text => Localize_Text.GetLocalizedString();
}
