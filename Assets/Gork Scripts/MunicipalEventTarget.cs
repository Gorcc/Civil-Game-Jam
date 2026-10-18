using UnityEngine;

public class MunicipalEventTarget : MonoBehaviour
{
    public MunicipalEventManager manager;
    public MunicipalEventManager.Job job;

    public void Open()
    {
        if (manager == null || job == null) return;
        var ui = manager.GetComponent<MunicipalEventUI>();
        if (ui != null) ui.OpenJob(job);
    }
}
