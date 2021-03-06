﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Idevgame.GameState.DialogState;

public class LoadingDialogState:CommonDialogState<LoadingUiController>
{
    public override string DialogName { get { return "LoadingWnd"; } }
    public LoadingDialogState(MainDialogStateManager dialog):base(dialog)
    {

    }
}

public class LoadingUiController : Dialog
{
    [SerializeField]
    Image mProgressBar;
    [SerializeField]
    Image ShowLoading;
    [SerializeField]
    Text mProgessLab;
    [SerializeField]
    Text LoadingNoticeLabel;
    public override void OnDialogStateEnter(BaseDialogState ownerState, BaseDialogState previousDialog, object data)
    {
        base.OnDialogStateEnter(ownerState, previousDialog, data);
        Material loadingTexture = null;
        if (Global.Instance.Chapter == null)
        {
            if (Global.Instance.GLevelItem != null && !string.IsNullOrEmpty(Global.Instance.GLevelItem.BgTexture))
                loadingTexture = GameObject.Instantiate(Resources.Load<Material>(Global.Instance.GLevelItem.BgTexture)) as Material;
            else
                loadingTexture = GameObject.Instantiate(Resources.Load<Material>("Scene10")) as Material;
        }
        else
        {
            for (int i = 0; i < Global.Instance.Chapter.resPath.Length; i++)
            {
                if (Global.Instance.Chapter.resPath[i].EndsWith(Global.Instance.GLevelItem.BgTexture + ".jpg"))
                {
                    byte[] array = System.IO.File.ReadAllBytes(Global.Instance.Chapter.resPath[i]);
                    Texture2D tex = new Texture2D(0, 0);
                    tex.LoadImage(array);
                    loadingTexture = GameObject.Instantiate(Resources.Load<Material>("Scene10")) as Material;
                    loadingTexture.SetTexture("_MainTex", tex);
                    loadingTexture.SetColor("_TintColor", Color.white);
                    break;
                }
            }
        }
        if (loadingTexture != null)
            ShowLoading.material = loadingTexture;
        SetLoadingNoticeLabel();
        mProgressBar.fillAmount = 0f;
    }

    public void UpdateProgress(float progress)
    {
        mProgressBar.fillAmount = progress;
        mProgessLab.text = string.Format("{0:P0}", progress);
    }


    public void SetLoadingNoticeLabel()
    {
        LoadingNoticeLabel.text = "";
    }
}
