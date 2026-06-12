using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleSettings : MonoBehaviour
{
    public GameObject SettingsPanel;

    public void TogglePanel()
    {
        SettingsPanel.SetActive(!SettingsPanel.activeInHierarchy);
    }
}
