using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class PatchBackground : MonoBehaviour
{
    [SerializeField] bool _isLauncher = false;
    async void Start()
    {
        await System.Threading.Tasks.Task.CompletedTask;
    }
}
