﻿using UnityEngine;
using System.Collections.Generic;
using System.Collections;

using System;
using protocol;

public enum SceneEvent
{
    EventEnter = 200,//进入传送门
    EventExit = 201,//离开传送门
    EventDeath = 202,//死亡事件
}

public class BattleResultItem
{
    public int camp;
    public int deadCount;
    public int killCount;
    public int id;
}

//负责战斗中相机的位置指定之类，主角色目标组作为摄像机 视锥体范围，参考Tank教程里的简单相机控制
//负责战斗场景内位置间的寻路缓存
public partial class GameBattleEx : NetBehaviour {
    public static GameBattleEx Instance;
    int time = 1000;//秒
    float timeClock = 0.0f;
    const float ViewLimit = 40000;//300码，自动解除锁定.
    public new void Awake()
    {
        base.Awake();
        Instance = this;
#if !STRIP_DBG_SETTING
        WSDebug.Ins.AddDebuggableObject(this);
#endif
    }

    public new void OnDestroy()
    {
#if !STRIP_DBG_SETTING
        if (WSDebug.Ins != null)
            WSDebug.Ins.RemoveDebuggableObject(this);
#endif
        Instance = null;
        base.OnDestroy();
    }

    public bool BattleWin()
    {
        return Result == 1 || Result == 2;
    }

    public bool BattleLose()
    {
        return Result <= 0 && Result != -10;
    }

    public bool BattleTimeup()
    {
        return Result == 3;
    }

    public bool BattleFinished()
    {
        return Result != -10;
    }

    int Result = -10;
    bool showResult = false;
    float showResultTick = 0.0f;
    //显示失败，或者胜利界面 (2 >= x >= 1 = win)  (x <= 0 = lose) (x == 3) = none
    public void GameOver(int result)
    {
        if (Result != -10)
            return;

        if (Global.Instance.GLevelMode == LevelMode.MultiplyPlayer)
        {
            //等待服务器再开一局.逻辑先不处理.
            return;
        }

        Global.Instance.PauseAll = true;
#if !STRIP_DBG_SETTING
        ShowWayPoint(false);
#endif
        MeteorManager.Instance.LocalPlayer.controller.LockInput(true);
        GameObject.Destroy(Main.Instance.playerListener);
        Main.Instance.playerListener = null;
        Main.Instance.listener.enabled = true;
        if (CameraFree.Ins != null && CameraFree.Ins.enabled)
        {
            EnableFreeCamera(false);
            EnableFollowCamera(true);
        }
        SoundManager.Instance.StopAll();
        //关闭界面的血条缓动和动画
        //if (FightWnd.Exist)
        //    FightWnd.Instance.OnBattleEnd();
        //if (SfxWnd.Exist)
        //    SfxWnd.Instance.Close();
        //if (EscWnd.Exist)
        //    EscWnd.Instance.Close();
        
        Result = result;

        showResult = true;
        showResultTick = 0;

        if (NGUICameraJoystick.instance)
            NGUICameraJoystick.instance.Lock(true);
        if (NGUIJoystick.instance)
            NGUIJoystick.instance.Lock(true);
        //如果胜利，且不是最后一关，打开最新关标志.
        if (result == 1 || result == 2)
        {
            if (Global.Instance.Chapter == null)
            {
                GameData.Instance.gameStatus.Level++;
                if (GameData.Instance.gameStatus.Level >= Global.Instance.LEVELMAX)
                    GameData.Instance.gameStatus.Level = Global.Instance.LEVELMAX;
                GameData.Instance.SaveState();
            }
            else
            {
                int nextLevel = 0;
                Level[] all = Global.Instance.Chapter.LoadAll();
                for (int i = 0; i < all.Length; i++)
                {
                    if (Global.Instance.GLevelItem.ID == all[i].ID)
                    {
                        nextLevel = i + 1;
                        break;
                    }
                }

                if (nextLevel < all.Length && Global.Instance.Chapter.level < all[nextLevel].ID)
                    Global.Instance.Chapter.level = all[nextLevel].ID;

                GameData.Instance.SaveState();
            }
        }
    }

    //void PlayEndMovie()
    //{
    //    //if (!string.IsNullOrEmpty(Global.GLevelItem.sceneItems))
    //    //{
    //    //    string num = Global.GLevelItem.sceneItems.Substring(2);
    //    //    int number = 0;
    //    //    if (int.TryParse(num, out number))
    //    //    {
    //    //        Debug.Log("v" + number);
    //    //        U3D.PlayMovie("v" + number);
    //    //    }
    //    //}
    //    GotoMenu();
    //}

    public int GetMiscGameTime()
    {
        return Mathf.FloorToInt(1000 * timeClock);
    }

    public int GetGameTime()
    {
        return Mathf.FloorToInt(timeClock);
    }
    
    //多久触发一次.
    void UpdateTime()
    {
        if (FightDialogState.Exist())
            FightDialogState.Instance.UpdateTime(GetTimeClock());
    }

    GameObject SelectTarget;
    float timeDelay = 1;
    List<int> UnitActKeyDeleted = new List<int>();
    //战斗场景时，需要每一帧执行的.
    public override void NetUpdate()
    {
        if (showResult)
        {
            if (showResultTick >= 3.0f)
            {
                showResultTick = 0.0f;
                showResult = false;
                //打开结算面板
                Main.Instance.DialogStateManager.ChangeState(Main.Instance.DialogStateManager.BattleResultDialogState);
                BattleResultDialogState.Instance.SetResult(this.Result);
            }
            showResultTick += FrameReplay.deltaTime;
            return;
        }

        if (Global.Instance.PauseAll)
            return;

        if (Global.Instance.GLevelMode <= LevelMode.CreateWorld)
        {
            timeClock += FrameReplay.deltaTime;
            if (timeClock > (float)time)//时间超过，平局
            {
                GameOver((int)GameResult.TimeOut);
                return;
            }
        }

        //检查盟主模式下的死亡单位，令其复活
        if (Global.Instance.GGameMode == GameMode.MENGZHU)
        {
            MeteorManager.Instance.DeadUnitsClone.Clear();
            for (int i = 0; i < MeteorManager.Instance.DeadUnits.Count; i++)
            {
                MeteorManager.Instance.DeadUnitsClone.Add(MeteorManager.Instance.DeadUnits[i]);
            }
            for (int i = 0; i < MeteorManager.Instance.DeadUnitsClone.Count; i++)
            {
                MeteorManager.Instance.DeadUnitsClone[i].RebornUpdate();
            }
        }

        //更新BUFF
        foreach (var each in BuffMng.Instance.BufDict)
            each.Value.NetUpdate();

        //更新左侧角色对话文本
        for (int i = 0; i < UnitActKey.Count; i++)
        {
            if (UnitActionStack.ContainsKey(UnitActKey[i]))
            {
                //StackAction act = UnitActionStack[UnitActKey[i]].action[UnitActionStack[UnitActKey[i]].action.Count - 1].type;
                UnitActionStack[UnitActKey[i]].Update(Time.deltaTime);
                if (UnitActionStack[UnitActKey[i]].action.Count == 0)
                {
                    //MeteorUnit unit = U3D.GetUnit(UnitActKey[i]);
                    //Debug.Log(string.Format("{0} action call finished:{1}", unit.name, act));
                    UnitActKeyDeleted.Add(UnitActKey[i]);
                }
            }
        }

        if (UnitActKeyDeleted.Count != 0)
        {
            for (int i = 0; i < UnitActKeyDeleted.Count; i++)
            {
                UnitActKey.Remove(UnitActKeyDeleted[i]);
                UnitActionStack.Remove(UnitActKeyDeleted[i]);
            }
            UnitActKeyDeleted.Clear();
        }

        if (Global.Instance.GScript != null && Global.Instance.GLevelMode < LevelMode.CreateWorld)
        {
            if (timeDelay >= 1.0f)
            {
                Global.Instance.GScript.OnUpdate();
                timeDelay = 0.0f;
            }
        }

        timeDelay += FrameReplay.deltaTime;
        if (Global.Instance.GScript != null)
            Global.Instance.GScript.Scene_OnIdle();//每一帧调用一次场景物件更新.
        RefreshAutoTarget();
        CollisionCheck();
        UpdateTime();
    }

    //每一个攻击盒，与所有受击盒碰撞测试.
    Dictionary<MeteorUnit, List<MeteorUnit>> hitDic = new Dictionary<MeteorUnit, List<MeteorUnit>>();
    //每个攻击盒，与所有环境物受击盒碰撞测试
    Dictionary<MeteorUnit, List<SceneItemAgent>> hitDic2 = new Dictionary<MeteorUnit, List<SceneItemAgent>>();

    private void CollisionCheck()
    {
        hitDic.Clear();
        hitDic2.Clear();
        foreach (var attack in DamageList)
        {
            //攻击盒与角色受击盒碰撞
            foreach (var each in MeteorManager.Instance.UnitInfos)
            {
                if (attack.Key == each)
                    continue;
                //同阵营不计算碰撞
                if (attack.Key.SameCamp(each))
                    continue;
                //已经发生过碰撞不再次计算
                if (attack.Key.ExistDamage(each))
                    continue;
                //buff无法攻击的没考虑.
                //不允许碰撞多次.
                if (hitDic.ContainsKey(attack.Key) && hitDic[attack.Key].Contains(each))
                    continue;
                if (OnHitTest(attack.Value, each))
                {
                    //Debug.LogError("OnHitTest");
                    if (hitDic.ContainsKey(attack.Key))
                        hitDic[attack.Key].Add(each);
                    else
                        hitDic.Add(attack.Key, new List<MeteorUnit> { each });
                    //攻击盒打受击盒
                    //attack.Key.Attack(each);
                    //受击盒
                    //each.OnAttack(attack.Key);
                }
            }

            //攻击盒与环境物件碰撞
            foreach (var each in Collision)
            {
                if (attack.Key.ExistDamage(each.Key))
                    continue;
                if (OnHitTest(attack.Value, each.Value))
                {
                    if (hitDic2.ContainsKey(attack.Key))
                        hitDic2[attack.Key].Add(each.Key);
                    else
                        hitDic2.Add(attack.Key, new List<SceneItemAgent> { each.Key });
                }
            }
        }  
        foreach (var each in hitDic)
        {
            for (int i = 0; i < each.Value.Count; i++)
            {
                each.Key.Attack(each.Value[i]);
                each.Value[i].OnAttack(each.Key);
            }
        }
        foreach (var each in hitDic2)
        {
            for (int i = 0; i < each.Value.Count; i++)
            {
                each.Key.Attack(each.Value[i]);
                each.Value[i].OnDamage(each.Key);
            }
        }

        //处理推挤问题。
    }

    public void OnSFXDestroy(MeteorUnit unit, Collider co)
    {
        if (DamageList.ContainsKey(unit))
            DamageList[unit].Remove(co);
    }

    public bool OnHitTest(List<Collider> colist, MeteorUnit ondamaged)
    {
        for (int i = 0; i < colist.Count; i++)
        {
            if (colist[i] == null)//协程中删除
            {
                colist.RemoveAt(i);
                continue;
            }
            if (!colist[i].enabled)
                continue;
            for (int j = 0; j < ondamaged.hitList.Count; j++)
            {
                if (!ondamaged.hitList[j].enabled)
                    continue;
                if (colist[i].bounds.Intersects(ondamaged.hitList[j].bounds))
                    return true;
            }
        }
        return false;
    }

    public bool OnHitTest(List<Collider> colist, List<Collider> ondamaged)
    {
        for (int i = 0; i < colist.Count; i++)
        {
            if (colist[i] == null || colist[i].gameObject == null)
                continue;
            if (!colist[i].enabled)
                continue;
            for (int j = 0; j < ondamaged.Count; j++)
            {
                if (ondamaged[j] == null)
                    continue;
                if (!ondamaged[j].enabled)
                    return false;
                if (colist[i].bounds.Intersects(ondamaged[j].bounds))
                    return true;
            }
        }
        return false;
    }

    //bool IsUIAction()
    //{
    //    LayerMask maskNgui = 1 << LayerMask.NameToLayer("NGUI") | 1 << LayerMask.NameToLayer("UI");
    //    //先看是否触发了UI控件.是则不选择对象
    //    Ray nguiRay = GameObject.Find("Camera").GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
    //    RaycastHit[] UISelection = Physics.RaycastAll(nguiRay, 100, maskNgui.value);
    //    if (UISelection.Length != 0)
    //        return true;
    //    return false;
    //}

    //bool IsSelectAction(ref GameObject touched)
    //{
    //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    //    LayerMask mask = 1 << LayerMask.NameToLayer("LocalPlayer") | 1 << LayerMask.NameToLayer("Trigger");
    //    RaycastHit[] UISelection = Physics.RaycastAll(ray, 100, mask.value);
    //    if (UISelection.Length != 0)
    //    {
    //        float fdisMin = float.MaxValue;
    //        for (int i = 0; i < UISelection.Length; i++)
    //        {
    //            if (UISelection[i].distance <= fdisMin)
    //            {
    //                fdisMin = UISelection[i].distance;
    //                touched = UISelection[i].collider.gameObject;
    //            }
    //        }
    //        return true;
    //    }
    //    return false;
    //}

    System.Reflection.MethodInfo Scene_OnCharacterEvent;
    System.Reflection.MethodInfo Scene_OnEvent;
    int EventDeath = 202;
    public void Init(LevelScriptBase script)
    {
        Scene_OnCharacterEvent = Global.Instance.GScriptType.GetMethod("Scene_OnCharacterEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (Scene_OnCharacterEvent == null)
        {
            System.Type typeParent = Global.Instance.GScriptType.BaseType;
            while (Scene_OnCharacterEvent == null && typeParent != null)
            {
                Scene_OnCharacterEvent = typeParent.GetMethod("Scene_OnCharacterEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                typeParent = typeParent.BaseType;
            }
        }
        Scene_OnEvent = Global.Instance.GScriptType.GetMethod("Scene_OnEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (Scene_OnEvent == null)
        {
            System.Type typeParent = Global.Instance.GScriptType.BaseType;
            while (Scene_OnEvent == null && typeParent != null)
            {
                Scene_OnEvent = typeParent.GetMethod("Scene_OnEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                typeParent = typeParent.BaseType;
            }
        }

        DisableCameraLock = !GameData.Instance.gameStatus.AutoLock;
        //updateFn = ScriptMng.ins.GetFunc("OnUpdate");
        if (script != null)
        {
            if (Global.Instance.GLevelMode <= LevelMode.SinglePlayerTask)
                time = script.GetRoundTime() * 60;
            else if (Global.Instance.GLevelMode == LevelMode.CreateWorld)
                time = Global.Instance.RoundTime * 60;
            else
                time = 30 * 60;
        }
        timeClock = 0.0f;
        //上一局如果暂停了，新局开始都恢复
        Resume();

        if (Global.Instance.GScript != null)
        {
            Global.Instance.GScript.Scene_OnLoad();
            Global.Instance.GScript.Scene_OnInit();
        }

        for (int i = 0; i < MeteorManager.Instance.SceneItems.Count; i++)
        {
            MeteorManager.Instance.SceneItems[i].OnStart(Global.Instance.GScript);
            RegisterCollision(MeteorManager.Instance.SceneItems[i]);
        }

        if (Global.Instance.GLevelMode <= LevelMode.CreateWorld)
        {
            OnCreatePlayer();

            if (Global.Instance.GScript != null && Global.Instance.GLevelMode <= LevelMode.SinglePlayerTask)
                Global.Instance.GScript.OnStart();

            for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
            {
                if (MeteorManager.Instance.UnitInfos[i].Attr != null)
                    MeteorManager.Instance.UnitInfos[i].Attr.OnStart();//AI脚本必须在所有角色都加载完毕后再调用
            }

            //单机剧本时游戏开始前最后给脚本处理事件的机会,
            if (Global.Instance.GScript != null && Global.Instance.GLevelMode <= LevelMode.SinglePlayerTask)
                Global.Instance.GScript.OnLateStart();
        }
        else
        {
            if (Global.Instance.GGameMode == GameMode.ANSHA)
            {
                MeteorUnit uEnemy = U3D.GetTeamLeader(EUnitCamp.EUC_ENEMY);
                SFXLoader.Instance.PlayEffect("vipblue.ef", MeteorManager.Instance.LocalPlayer.gameObject, false);
                if (uEnemy != null)
                    SFXLoader.Instance.PlayEffect("vipred.ef", uEnemy.gameObject, false);
            }
        }

        CrateCamera();
    }

    void CrateCamera()
    {
        //先创建一个相机
        GameObject camera = GameObject.Instantiate(Resources.Load("CameraEx")) as GameObject;
        camera.name = "CameraEx";

        //角色摄像机跟随者着角色.
        CameraFollow followCamera = GameObject.Find("CameraEx").GetComponent<CameraFollow>();
        followCamera.Init();
        followCamera.FollowTarget(MeteorManager.Instance.LocalPlayer.transform);
    }

    void SpawnAllRobot()
    {
        if (Global.Instance.GGameMode == GameMode.MENGZHU)
        {
            for (int i = 1; i < Global.Instance.MaxPlayer; i++)
            {
                U3D.SpawnRobot(U3D.GetRandomUnitIdx(), EUnitCamp.EUC_KILLALL, GameData.Instance.gameStatus.Single.DisallowSpecialWeapon ? U3D.GetNormalWeaponType() : U3D.GetRandomWeaponType(), Global.Instance.PlayerLife);
            }
        }
        else if (Global.Instance.GGameMode == GameMode.ANSHA || Global.Instance.GGameMode == GameMode.SIDOU)
        {
            int FriendCount = Global.Instance.MaxPlayer / 2 - 1;
            for (int i = 0; i < FriendCount; i++)
            {
                U3D.SpawnRobot(U3D.GetRandomUnitIdx(), MeteorManager.Instance.LocalPlayer.Camp, GameData.Instance.gameStatus.Single.DisallowSpecialWeapon ? U3D.GetNormalWeaponType() : U3D.GetRandomWeaponType(), Global.Instance.PlayerLife);
            }

            for (int i = FriendCount + 1; i < Global.Instance.MaxPlayer; i++)
            {
                U3D.SpawnRobot(U3D.GetRandomUnitIdx(), U3D.GetAnotherCamp(MeteorManager.Instance.LocalPlayer.Camp), GameData.Instance.gameStatus.Single.DisallowSpecialWeapon ? U3D.GetNormalWeaponType() : U3D.GetRandomWeaponType(), Global.Instance.PlayerLife);
            }
        }
    }

    public void OnCreateNetPlayer(PlayerEventData player)
    {
        MeteorUnit unit = U3D.InitNetPlayer(player);
        U3D.InsertSystemMsg(U3D.GetCampEnterLevelStr(unit));
    }
    //单机角色创建
    public void OnCreatePlayer()
    {
        //设置主角属性
        U3D.InitPlayer(Global.Instance.GScript);
        //把音频侦听移到角色
        Main.Instance.listener.enabled = false;
        Main.Instance.playerListener = MeteorManager.Instance.LocalPlayer.gameObject.AddComponent<AudioListener>();

        if (Global.Instance.GLevelMode == LevelMode.CreateWorld)
            SpawnAllRobot();

        //摄像机完毕后
        //FightWnd.Instance.Open();
        if (!string.IsNullOrEmpty(Global.Instance.GLevelItem.BgmName))
            SoundManager.Instance.PlayMusic(Global.Instance.GLevelItem.BgmName);

        //除了主角的所有角色,开始输出,选择阵营, 进入战场
        for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
        {
            if (MeteorManager.Instance.UnitInfos[i] == MeteorManager.Instance.LocalPlayer)
                continue;
            MeteorUnit unitLog = MeteorManager.Instance.UnitInfos[i];
            U3D.InsertSystemMsg(U3D.GetCampEnterLevelStr(unitLog));
        }

        U3D.InsertSystemMsg("新回合开始计时");
        if (FightDialogState.Exist())
            FightDialogState.Instance.OnBattleStart();
    }

    //场景物件的受击框
    Dictionary<SceneItemAgent, List<Collider>> Collision = new Dictionary<SceneItemAgent, List<Collider>>();
    public void RegisterCollision(SceneItemAgent agent)
    {
        if (!Collision.ContainsKey(agent))
        {
            if (agent.HasCollision())
                Collision.Add(agent, agent.GetCollsion());
        }
        else
        {
            if (agent.HasCollision())
                Collision[agent] = agent.GetCollsion();
            else
                Collision.Remove(agent);
        }
    }

    public void RemoveCollision(SceneItemAgent agent)
    {
        if (agent != null && Collision.ContainsKey(agent))
            Collision.Remove(agent);
    }

    RaycastHit [] SortRaycastHit(RaycastHit [] hit)
    {
        int index = 0;
        while (true)
        {
            //每次得到一个最小的插槽，与最前面一个调换位置.
            int slot = -1;
            float minDistance = float.MaxValue;
            for (int i = index; i < hit.Length; i++)
            {
                if (hit[i].distance < minDistance)
                {
                    minDistance = hit[i].distance;
                    slot = i;
                }
            }
            if (slot != -1 && slot != index)
            {
                RaycastHit ray = hit[index];
                hit[index] = hit[slot];
                hit[slot] = ray;
            }
            index++;
            if (index >= hit.Length)//火枪BUG修复
                break;
        }
        return hit;
    }

    List<MeteorUnit> hitTarget = new List<MeteorUnit>();
    List<SceneItemAgent> hitItem = new List<SceneItemAgent>();
    public void AddDamageCheck(MeteorUnit unit, AttackDes attackDef)
    {
        if (unit == null || unit.Dead)
            return;
        hitTarget.Clear();
        hitItem.Clear();
        if (unit.GetWeaponType() == (int)EquipWeaponType.Gun)
        {
            if (unit.Attr.IsPlayer)
            {
                Ray r = CameraFollow.Ins.m_Camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, DartLoader.MaxDistance));
                RaycastHit[] allHit = Physics.RaycastAll(r, DartLoader.MaxDistance, 1 << LayerMask.NameToLayer("Bone") | 1 << LayerMask.NameToLayer("Scene") | 1 << LayerMask.NameToLayer("Monster") | 1 << LayerMask.NameToLayer("LocalPlayer") | 1 << LayerMask.NameToLayer("Trigger"));
                RaycastHit[] allHitSort = SortRaycastHit(allHit);
                bool showEffect = false;
                //先排个序，从近到远
                for (int i = 0; i < allHitSort.Length; i++)
                {
                    if (allHitSort[i].transform.root.gameObject.layer == LayerMask.NameToLayer("Scene"))
                    {
                        //主角可以击中
                        GameObject other = allHitSort[i].transform.gameObject;
                        SceneItemAgent it = other.GetComponentInParent<SceneItemAgent>();
                        if (it != null)
                        {
                            //Debug.Log("dart attack sceneitemagent");
                            if (!hitItem.Contains(it))
                            {
                                it.OnDamage(unit, attackDef);
                                hitItem.Add(it);
                            }
                        }
                        if (!showEffect)
                        {
                            SFXLoader.Instance.PlayEffect("GunHit.ef", allHitSort[i].point, true, false);
                            showEffect = true;
                        }
                        break;
                    }

                    MeteorUnit unitAttacked = allHitSort[i].transform.gameObject.GetComponentInParent<MeteorUnit>();
                    if (unitAttacked != null)
                    {
                        if (unit == unitAttacked)
                            continue;
                        if (unit.SameCamp(unitAttacked))
                            continue;
                        if (unitAttacked.Dead)
                            continue;
                        if (!hitTarget.Contains(unitAttacked))
                        {
                            //在指定位置播放一个特效.
                            if (!showEffect)
                            {
                                SFXLoader.Instance.PlayEffect("GunHit.ef", allHitSort[i].point, true, false);
                                showEffect = true;
                            }
                            unitAttacked.OnAttack(unit, attackDef);
                            hitTarget.Add(unitAttacked);
                        }
                    }
                }
            }
            else
            {
                //火枪射线应该由，持枪点朝目标处，直接用概率算。
                GameObject gun = unit.weaponLoader.GetGunTrans();
                if (gun != null)
                {
                    Vector3 vec = Vector3.zero;
                    if (unit.GetLockedTarget() != null)
                    {
                        Vector3 vecDir = (unit.GetLockedTarget().mSkeletonPivot - gun.transform.position);
                        vec = vecDir;
                        vec.y = 0;
                        Vector3 vecShootEndPoint = unit.GetLockedTarget().mSkeletonPivot + Quaternion.AngleAxis(90, Vector3.up) * vec.normalized * unit.charController.radius;//角色某侧顶端
                        float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot((vecShootEndPoint - gun.transform.position).normalized, vecDir.normalized), -1.0f, 1.0f)) * Mathf.Rad2Deg;
                        float ratio = ((100.0f - unit.Attr.Aim) / 100.0f);//miss概率                                            //正负1倍角度一定可以射击命中.不在范围内一定不会命中.
                        float angleRatio = UnityEngine.Random.Range(-angle * (1 + ratio), angle * (1 + ratio));
                        vec = Quaternion.AngleAxis(angleRatio, Vector3.up) * (unit.GetLockedTarget().mSkeletonPivot - gun.transform.position).normalized;
                    }
                    else
                        vec = -gun.transform.forward;
                    Ray r = new Ray(gun.transform.position, vec);
                    RaycastHit[] allHit = Physics.RaycastAll(r, 3000, 1 << LayerMask.NameToLayer("Bone") | 1 << LayerMask.NameToLayer("Scene") | 1 << LayerMask.NameToLayer("Monster") | 1 << LayerMask.NameToLayer("LocalPlayer"));
                    RaycastHit[] allHitSort = SortRaycastHit(allHit);
                    bool showEffect = false;
                    //先排个序，从近到远
                    for (int i = 0; i < allHitSort.Length; i++)
                    {
                        if (allHitSort[i].transform.root.gameObject.layer == LayerMask.NameToLayer("Scene"))
                        {
                            //在指定位置播放一个特效.
                            if (!showEffect)
                            {
                                SFXLoader.Instance.PlayEffect("GunHit.ef", allHitSort[i].point, true, false);
                                showEffect = true;
                            }
                            break;
                        }

                        MeteorUnit unitAttacked = allHitSort[i].transform.gameObject.GetComponentInParent<MeteorUnit>();
                        if (unitAttacked != null)
                        {
                            if (unit == unitAttacked)
                                continue;
                            if (unit.SameCamp(unitAttacked))
                                continue;
                            if (unitAttacked.Dead)
                                continue;
                            if (!hitTarget.Contains(unitAttacked))
                            {
                                //在指定位置播放一个特效.
                                if (!showEffect)
                                {
                                    SFXLoader.Instance.PlayEffect("GunHit.ef", allHitSort[i].point, true, false);
                                    showEffect = true;
                                }
                                unitAttacked.OnAttack(unit, attackDef);
                                hitTarget.Add(unitAttacked);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            //飞镖，那么从绑点生成一个物件，让他朝
            //"Sphere3"是挂点
            //即刻生成一个物件飞出去。
            if (unit.charLoader.sfxEffect != null)
            {
                if (unit.GetWeaponType() == (int)EquipWeaponType.Dart)
                {
                    //飞镖.
                    Transform bulletBone = unit.charLoader.sfxEffect.FindEffectByName("Sphere_3");//出生点，
                    Vector3 vecSpawn = bulletBone.position;
                    Vector3 forw = Vector3.zero;
                    if (unit.Attr.IsPlayer)
                    {
                        //远程锁定目标为空时
                        if (lockedTarget2 == null)
                        {
                            Vector3 vec = CameraFollow.Ins.m_Camera.ScreenToWorldPoint(new Vector3(Screen.width / 2, (Screen.height) * 0.65f, DartLoader.MaxDistance));
                            forw = (vec - vecSpawn).normalized;
                        }
                        else
                        {
                            Vector3 vec = lockedTarget2.mSkeletonPivot;
                            forw = (vec - vecSpawn).normalized;
                        }
                    }
                    else
                    {
                        //AI在未发现敌人时随便发飞镖?
                        if (unit.GetLockedTarget() == null)
                        {
                            //角色的面向 + 一定随机
                            forw = (Quaternion.AngleAxis(UnityEngine.Random.Range(-35, 35), Vector3.up)  * Quaternion.AngleAxis(UnityEngine.Random.Range(-5, 5), Vector3.right)  * - unit.transform.forward).normalized;
                        }
                        else
                        {
                            //判断角色是否面向目标，不面向，则朝身前发射飞镖
                            MeteorUnit u = unit.GetLockedTarget();
                            if (u != null)
                            {
                                if (!unit.IsFacetoTarget(u))
                                    u = null;
                            }

                            if (u == null)
                                forw = (Quaternion.AngleAxis(UnityEngine.Random.Range(-35, 35), Vector3.up) * Quaternion.AngleAxis(UnityEngine.Random.Range(-5, 5), Vector3.right) * -unit.transform.forward).normalized;//角色的面向
                            else
                            {
                                float k = UnityEngine.Random.Range(-(100 - unit.Attr.Aim) / 3.0f, (100 - unit.Attr.Aim) / 3.0f);
                                Vector3 vec = unit.GetLockedTarget().transform.position + new Vector3(k, UnityEngine.Random.Range(10, 38), k);
                                forw = (vec - vecSpawn).normalized;
                            }
                            //要加一点随机，否则每次都打一个位置不正常
                        }
                    }
                    //主角则方向朝着摄像机正前方
                    //不是主角没有摄像机，那么就看有没有目标，有则随机一个方向，根据与目标的包围盒的各个顶点的，判定这个方向
                    //得到武器模型，在指定位置出生.
                    InventoryItem weapon = unit.weaponLoader.GetCurrentWeapon();
                    DartLoader.Init(vecSpawn, forw, weapon, attackDef, unit);
                }
                else if (unit.GetWeaponType() == (int)EquipWeaponType.Guillotines)
                {
                    //隐藏右手的飞轮
                    unit.weaponLoader.HideWeapon();
                    //飞轮
                    Vector3 vecSpawn = unit.WeaponR.transform.position;
                    InventoryItem weapon = unit.weaponLoader.GetCurrentWeapon();
                    if (unit.Attr.IsPlayer)
                        FlyWheel.Init(vecSpawn, autoTarget, weapon, attackDef, unit);
                    else
                    {
                        //判断角色是否面向目标，不面向，则朝身前发射飞轮
                        MeteorUnit u = unit.GetLockedTarget();
                        if (u != null)
                        {
                            if (!unit.IsFacetoTarget(u))
                                u = null;
                        }
                        
                        FlyWheel.Init(vecSpawn, u, weapon, attackDef, unit);
                    }
                }
            }
        }
    }

    //所有角色的攻击盒.特效，武器
    Dictionary<MeteorUnit, List<Collider>> DamageList = new Dictionary<MeteorUnit, List<Collider>>();
    //缓存角色的攻击盒.
    public void AddDamageCollision(MeteorUnit unit, Collider co)
    {
        if (unit == null || co == null)
            return;
        if (DamageList.ContainsKey(unit))
        {
            if (DamageList[unit].Contains(co))
                return;
            DamageList[unit].Add(co);
        }
        else
            DamageList.Add(unit, new List<Collider>() { co });
    }

    //清理角色的攻击盒.
    public void ClearDamageCollision(MeteorUnit unit)
    {
        if (DamageList.ContainsKey(unit))
            DamageList.Remove(unit);
    }

    public string GetTimeClock()
    {
        //600 = 10:00
        if (Global.Instance.GLevelMode <= LevelMode.CreateWorld)
        {
            int left = time - Mathf.FloorToInt(timeClock);
            if (left < 0)
                left = 0;
            int minute = left / 60;
            int seconds = left % 60;
            string t = "";
            t = string.Format("{0:D2}:{1:D2}", minute, seconds);
            return t;
        }
        else if (Global.Instance.GLevelMode == LevelMode.MultiplyPlayer)
        {
            int left = Mathf.FloorToInt(NetWorkBattle.Instance.GameTime / 1000);
            if (left < 0)
                left = 0;
            int minute = left / 60;
            int seconds = left % 60;
            string t = "";
            t = string.Format("{0:D2}:{1:D2}", minute, seconds);
            return t;
        }
        return "--:--";
    }

    public bool IsTimeup()
    {
        int left = time - Mathf.FloorToInt(timeClock);
        if (left < 0)
            return true;
        return false;
    }
    
    public void NetPause()
    {
        if (MeteorManager.Instance.LocalPlayer != null)
            MeteorManager.Instance.LocalPlayer.controller.LockInput(true);
        if (NGUIJoystick.instance != null)
            NGUIJoystick.instance.Lock(true);
        if (NGUICameraJoystick.instance != null)
            NGUICameraJoystick.instance.Lock(true);
        for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
            MeteorManager.Instance.UnitInfos[i].EnableAI(false);
        Global.Instance.PauseAll = true;
    }

    //全部AI暂停，游戏时间停止，任何依据时间做动画的物件，全部要停止.
    public void Pause()
    {
        //联机时不许暂停
        if (Global.Instance.GLevelMode == LevelMode.MultiplyPlayer)
            return;

        NetPause();
    }

    public void Resume()
    {
        if (MeteorManager.Instance.LocalPlayer != null)
        {
            MeteorManager.Instance.LocalPlayer.controller.LockInput(false);
        }
        if (NGUIJoystick.instance != null)
            NGUIJoystick.instance.Lock(false);
        if (NGUICameraJoystick.instance != null)
            NGUICameraJoystick.instance.Lock(false);
        for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
            MeteorManager.Instance.UnitInfos[i].EnableAI(true);
        Global.Instance.PauseAll = false;
    }

    public MeteorUnit lockedTarget;//近战武器-自动锁定目标
    public MeteorUnit lockedTarget2;//远程武器-自动锁定目标
    public MeteorUnit autoTarget;
    public SFXEffectPlay lockedEffect;
    public SFXEffectPlay autoEffect;
    public bool CanLockTarget(MeteorUnit unit)
    {
        //使用枪械/远程武器时
        int weapon = MeteorManager.Instance.LocalPlayer.GetWeaponType();
        if (weapon == (int) EquipWeaponType.Dart || 
            weapon == (int)EquipWeaponType.Gun || 
            weapon == (int)EquipWeaponType.Guillotines)
        {
            return false;
        }
        //只判断距离，因为匕首背刺不一定面对角色，但是在未有锁定目标时近距离打到敌人，则把敌人设置为目标.
        if (lockedTarget == null)
        {
            if (unit.Dead)
                return false;
            Vector3 vec = unit.transform.position - MeteorManager.Instance.LocalPlayer.transform.position;
            float v = vec.sqrMagnitude;
            if (v > ViewLimit)
                return false;
            return true;
        }
        return false;
    }
    //如果是空，标志着，锁定目标已经被消灭
    public void ChangeLockedTarget(MeteorUnit unit)
    {
        if (unit == null)
        {
            if (lockedEffect != null)
            {
                lockedEffect.OnPlayAbort();
                lockedEffect = null;
            }
            lockedTarget = null;
            bLocked = false;
            CameraFollow.Ins.OnChangeLockTarget(null);
        }
        else
        {
            if (autoEffect != null)
            {
                autoEffect.OnPlayAbort();
                autoEffect = null;
            }
            autoTarget = null;
            lockedTarget = unit;
            lockedEffect = SFXLoader.Instance.PlayEffect("lock.ef", lockedTarget.gameObject);
            bLocked = true;
            CameraFollow.Ins.OnChangeLockTarget(lockedTarget.transform);
        }
        //if (FightWnd.Exist)
        //    FightWnd.Instance.OnChangeLock(bLocked);
    }
    //存在自动目标时，把自动目标删除，然后设置其为锁定目标.
    public bool bLocked = false;
    public void Lock()
    {
        if (autoTarget != null)
        {
            MeteorUnit lockTarget = autoTarget;
            MeteorManager.Instance.LocalPlayer.SetLockedTarget(lockTarget);
            ChangeLockedTarget(lockTarget);
            CameraFollow.Ins.OnLockTarget();
        }
    }

    bool DisableCameraLock;
    public void DisableLock()
    {
        DisableCameraLock = true;
    }

    public void EnableLock()
    {
        DisableCameraLock = false;
    }

    //远程目标的解除锁定
    public void Unlock2()
    {
        if (lockedEffect != null)
        {
            lockedEffect.OnPlayAbort();
            lockedEffect = null;
        }
        lockedTarget2 = null;

        if (autoEffect != null)
        {
            autoEffect.OnPlayAbort();
            autoEffect = null;
        }
        autoTarget = null;
        //远程武器与摄像机锁定系统无关
    }

    public void Unlock()
    {
        if (lockedEffect != null)
        {
            lockedEffect.OnPlayAbort();
            lockedEffect = null;
        }
        lockedTarget = null;

        if (autoEffect != null)
        {
            autoEffect.OnPlayAbort();
            autoEffect = null;
        }
        autoTarget = null;
        MeteorManager.Instance.LocalPlayer.SetLockedTarget(null);
        CameraFollow.Ins.OnUnlockTarget();
        bLocked = false;
        //if (FightWnd.Exist)
        //    FightWnd.Instance.OnChangeLock(bLocked);
    }

    //远程武器自动锁定系统.血滴子算特殊武器，
    public void RefreshAutoTarget2()
    {
        //夹角超过限制
        MeteorUnit player = MeteorManager.Instance.LocalPlayer;
        float angleMax = 75;//cos值越大，角度越小
        float autoDis = ViewLimit;//自动目标与主角的距离，距离近，优先
        MeteorUnit wantRotation = null;//夹角最小的
        MeteorUnit wantDis = null;//距离最近的
        Vector3 vecPlayer = -player.transform.forward;
        vecPlayer.y = 0;
        for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
        {
            if (MeteorManager.Instance.UnitInfos[i] == player)
                continue;
            if (player.SameCamp(MeteorManager.Instance.UnitInfos[i]))
                continue;
            if (MeteorManager.Instance.UnitInfos[i].Dead)
                continue;
            Vector3 vec = MeteorManager.Instance.UnitInfos[i].transform.position - player.transform.position;
            float v = vec.sqrMagnitude;
            MeteorManager.Instance.UnitInfos[i].distance = v;
            vec.y = 0;
            //先判断夹角是否在限制范围内.
            vec = Vector3.Normalize(vec);
            float angle = Mathf.Acos(Vector3.Dot(vecPlayer.normalized, vec)) * Mathf.Rad2Deg;
            MeteorManager.Instance.UnitInfos[i].angle = angle;
            //角度小于75则可以成为自动对象.
            if (angle < angleMax)
            {
                angleMax = angle;
                wantRotation = MeteorManager.Instance.UnitInfos[i];
                
            }
            if (v < autoDis)
            {
                autoDis = v;
                wantDis = MeteorManager.Instance.UnitInfos[i];
            }
        }

        MeteorUnit finallyTarget = wantRotation;
        //如果一个角色较近，但是角度差更大，一个距离较远但是角度差较小，判定2者该挑选谁作为最终目标
        if (wantDis != null && wantDis != wantRotation && finallyTarget != null)
        {
            finallyTarget = wantRotation.TargetWeight() < wantDis.TargetWeight() ? wantRotation : wantDis;
            wantRotation = wantDis;
        }
                
        //与角色的夹角不能大于75度,否则主角脑袋骨骼可能注视不到自动目标.
        if (finallyTarget != autoTarget)
        {
            if (autoEffect != null)
            {
                autoEffect.OnPlayAbort();
                autoEffect = null;
            }
            autoTarget = finallyTarget;
            //Debug.Log("切换目标");
            if (autoTarget != null)
                autoEffect = SFXLoader.Instance.PlayEffect("Track.ef", autoTarget.gameObject);
            return;
        }

        //如果当前的自动目标存在，且夹角超过75度，即在主角背后，那么自动目标清空
        if (autoTarget != null && autoTarget.angle > 90)
        {
            autoEffect.OnPlayAbort();
            autoTarget = null;
            Debug.LogError("夹角太大，自动目标重置");
        }
    }

    //敌方角色移动时，不用整个刷新，只需要用当前的自动目标和这个对象对比下角度就OK
    public void RefreshAutoTarget()
    {
        if (MeteorManager.Instance.LocalPlayer == null)
            return;
        if (MeteorManager.Instance.LocalPlayer.GetWeaponType() == (int)EquipWeaponType.Dart || MeteorManager.Instance.LocalPlayer.GetWeaponType() == (int)EquipWeaponType.Gun)
        {
            //远程武器，自动锁定系统.
            RefreshAutoTarget2();
            lockedTarget2 = autoTarget;
            return;
        }
        //超出距离，并没有考虑角色背后-方向导致的解除锁定
        if (lockedTarget != null)
        {
            Vector3 vec = lockedTarget.transform.position - MeteorManager.Instance.LocalPlayer.transform.position;
            float v = vec.sqrMagnitude;
            if (v > ViewLimit)
                Unlock();
            return;
        }
        MeteorUnit player = MeteorManager.Instance.LocalPlayer;
        float angleMax = 75;//cos值越大，角度越小
        float autoAngle = 0.0f;//自动目标与主角的夹角
        float autoDis = ViewLimit;//自动目标与主角的距离，距离近，优先
        MeteorUnit wantRotation = null;//夹角最小的
        MeteorUnit wantDis = null;//距离最近的
        Vector3 vecPlayer = -player.transform.forward;
        vecPlayer.y = 0;
        for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
        {
            if (MeteorManager.Instance.UnitInfos[i] == player)
                continue;
            if (player.SameCamp(MeteorManager.Instance.UnitInfos[i]))
                continue;
            if (MeteorManager.Instance.UnitInfos[i].Dead)
                continue;
            Vector3 vec = MeteorManager.Instance.UnitInfos[i].transform.position - player.transform.position;
            float v = vec.sqrMagnitude;
            MeteorManager.Instance.UnitInfos[i].distance = v;
            //飞轮时，无限角度距离
            if (v > ViewLimit && MeteorManager.Instance.LocalPlayer.GetWeaponType() != (int)EquipWeaponType.Guillotines)
                continue;
            //高度差2个角色，不要成为自动对象，否则摄像机位置有问题
            if (Mathf.Abs(vec.y) >= 75 && MeteorManager.Instance.LocalPlayer.GetWeaponType() != (int)EquipWeaponType.Guillotines)
                continue;
            vec.y = 0;
            //先判断夹角是否在限制范围内.
            vec = Vector3.Normalize(vec);
            float angle = Mathf.Acos(Vector3.Dot(vecPlayer.normalized, vec)) * Mathf.Rad2Deg;
            MeteorManager.Instance.UnitInfos[i].angle = angle;
            //角度小于75则可以成为自动对象.
            if (angle < angleMax)
            {
                angleMax = angle;
                wantRotation = MeteorManager.Instance.UnitInfos[i];
            }
            if (v < autoDis)
            {
                autoDis = v;
                wantDis = MeteorManager.Instance.UnitInfos[i];
            }
        }

        MeteorUnit finallyTarget = wantRotation;
        //如果一个角色较近，但是角度差更大，一个距离较远但是角度差较小，判定2者该挑选谁作为最终目标
        if (wantDis != null && wantDis != wantRotation && finallyTarget != null)
        {
            finallyTarget = wantRotation.TargetWeight() < wantDis.TargetWeight() ? wantRotation : wantDis;
            wantRotation = wantDis;
        }
        //与角色的夹角不能大于75度,否则主角脑袋骨骼可能注视不到自动目标.
        if (finallyTarget != autoTarget)
        {
            if (autoEffect != null)
            {
                autoEffect.OnPlayAbort();
                autoEffect = null;
            }
            autoTarget = finallyTarget;
            if (autoTarget != null)
                autoEffect = SFXLoader.Instance.PlayEffect("Track.ef", autoTarget.gameObject);
            return;
        }

        //如果当前的自动目标存在，且夹角超过75度，即在主角背后，那么自动目标清空
        if (autoTarget != null && autoTarget.angle > 90 && MeteorManager.Instance.LocalPlayer.GetWeaponType() != (int)EquipWeaponType.Guillotines)
        {
            autoEffect.OnPlayAbort();
            autoTarget = null;
        }

        if (autoTarget != null && MeteorManager.Instance.LocalPlayer.GetWeaponType() != (int)EquipWeaponType.Guillotines)
        {
            if (autoTarget.distance > ViewLimit)
                Unlock();
        }
    }

    
    Dictionary<int, BattleResultItem> battleResult = new Dictionary<int, BattleResultItem>();
    public Dictionary<int, BattleResultItem> BattleResult { get { return battleResult; } }
    public void OnUnitDead(MeteorUnit unit, MeteorUnit killer = null)
    {
        if (Scene_OnCharacterEvent != null)
            Scene_OnCharacterEvent.Invoke(Global.Instance.GScript, new object[] { unit.InstanceId, EventDeath });
        //无阵营的角色,杀死人，不统计信息
        if (killer != null)
        {
            //统计杀人计数
            if (!battleResult.ContainsKey(killer.InstanceId))
            {
                BattleResultItem it = new BattleResultItem();
                it.camp = (int)killer.Camp;
                it.id = killer.InstanceId;
                it.deadCount = 0;
                it.killCount = 1;
                battleResult.Add(killer.InstanceId, it);
            }
            else
                battleResult[killer.InstanceId].killCount += 1;
        }

        if (unit != null)
        {
            //统计被杀次数
            if (!battleResult.ContainsKey(unit.InstanceId))
            {
                BattleResultItem it = new BattleResultItem();
                it.camp = (int)unit.Camp;
                it.id = unit.InstanceId;
                it.deadCount = 1;
                it.killCount = 0;
                battleResult.Add(unit.InstanceId, it);
            }
            else
                battleResult[unit.InstanceId].deadCount += 1;
        }

        if (unit == MeteorManager.Instance.LocalPlayer)
        {
            //如果是
            if (Global.Instance.GLevelMode == LevelMode.CreateWorld)
            {
                if (Global.Instance.GGameMode == GameMode.MENGZHU)
                {
                    //等一段时间后复活.
                }
                else if (Global.Instance.GGameMode == GameMode.ANSHA)
                {
                    //检查是否是队长
                    if (unit.IsLeader)
                        GameOver(0);
                }
                else if (Global.Instance.GGameMode == GameMode.SIDOU)
                {
                    //检查双方是否有一边全部战败
                    if (U3D.AllEnemyDead())
                        GameOver(1);
                    else if (U3D.AllFriendDead())
                        GameOver(0);
                    else
                    {
                        //用一个自由相机，拍摄场上PK的几个人
                        InitFreeCamera();
                        EnableFollowCamera(false);
                    }
                }
            }
            else
                GameOver(0);
            DropMng.Instance.Drop(unit);
            Unlock();
        }
        else
        {
            //无论谁杀死，都要爆东西
            DropMng.Instance.Drop(unit);
            //如果是被任意流星角色的伤害，特效，技能，等那么主角得到经验，如果是被场景环境杀死，
            //爆钱和东西
            //检查剧情
            //检查任务
            //检查过场对白，是否包含角色对话
            if (unit == autoTarget)
            {
                if (autoEffect != null)
                {
                    autoEffect.OnPlayAbort();
                    autoEffect = null;
                }
                autoTarget = null;
            }
            if (unit == lockedTarget)
                Unlock();
            if (Global.Instance.GLevelMode == LevelMode.SinglePlayerTask)
            {
                GameResult result = Global.Instance.GScript.OnUnitDead(unit);
                if (result != GameResult.None)
                    GameOver((int)result);
                //if (Global.Instance.GLevelItem.Pass == 1)//敌方阵营全死
                //{
                //    int totalEnemy = U3D.GetEnemyCount();
                //    if (totalEnemy == 0)
                //        GameOver(1);
                //}
                //else if (Global.Instance.GLevelItem.Pass == 2)//敌方指定脚本角色死亡
                //{
                //    if (unit.Attr.NpcTemplate == Global.Instance.GLevelItem.Param)
                //        GameOver(1);
                //}
            }
            //如果是
            else if (Global.Instance.GLevelMode == LevelMode.CreateWorld)
            {
                if (Global.Instance.GGameMode == GameMode.MENGZHU)
                {
                    //等一段时间后复活.
                }
                else if (Global.Instance.GGameMode == GameMode.ANSHA)
                {
                    //检查是否是队长
                    if (unit.IsLeader)
                        GameOver(1);
                }
                else if (Global.Instance.GGameMode == GameMode.SIDOU)
                {
                    //检查双方是否有一边全部战败
                    if (U3D.AllEnemyDead())
                        GameOver(1);
                    else if (U3D.AllFriendDead())
                        GameOver(0);
                }
            }
        }
    }

    public void EnableFollowCamera(bool enable)
    {
        if (CameraFollow.Ins != null)
        {
            CameraFollow.Ins.m_Camera.enabled = enable;
            CameraFollow.Ins.enabled = enable;
        }
    }

    public void EnableFreeCamera(bool enable)
    {
        if (CameraFree.Ins != null)
        {
            CameraFree.Ins.m_Camera.enabled = enable;
            CameraFree.Ins.enabled = enable;
        }
    }

    public void InitFreeCamera(MeteorUnit target = null)
    {
        if (CameraFree.Ins == null)
        {
            GameObject FreeCamera = GameObject.Instantiate(Resources.Load("CameraFreeEx")) as GameObject;
            FreeCamera.name = "FreeCamera";
        }
        CameraFree.Ins.GetComponent<Camera>().enabled = true;
        CameraFree.Ins.enabled = true;
        //找一个正在打斗的目标，随便谁都行
        MeteorUnit watchTarget = target;
        if (watchTarget == null)
        {
            for (int i = 0; i < MeteorManager.Instance.UnitInfos.Count; i++)
            {
                if (MeteorManager.Instance.UnitInfos[i].Dead)
                    continue;
                watchTarget = MeteorManager.Instance.UnitInfos[i];
                break;
            }
        }
        CameraFree.Ins.Init(watchTarget);
    }

    public void ChangeLockStatus()
    {
        if (lockedTarget != null)
            Unlock();
        //else if (autoTarget != null)
        //    Lock();
    }
    //每个角色拥有的动作堆栈
    Dictionary<int, ActionConfig> UnitActionStack = new Dictionary<int, ActionConfig>();
    List<int> UnitActKey = new List<int>();

    public bool IsPerforming(int unit)
    {
        return UnitActionStack.ContainsKey(unit);
    }

    public void PushActionSay(int id, string text)
    {
        PushAction(id, StackAction.SAY, 0, text);
    }

    public void PushActionPatrol(int id, List<int> path)
    {
        if (!UnitActKey.Contains(id))
            UnitActKey.Add(id);
        if (!UnitActionStack.ContainsKey(id))
        {
            UnitActionStack.Add(id, new ActionConfig());
            UnitActionStack[id].id = id;
        }
        ActionItem it = new ActionItem();
        it.pause_time = 0;
        it.type = StackAction.Patrol;
        it.Path = path;
        UnitActionStack[id].action.Add(it);
    }

    void PushActionUse(int id, StackAction type, int idx)
    {
        if (!UnitActKey.Contains(id))
            UnitActKey.Add(id);
        if (!UnitActionStack.ContainsKey(id))
        {
            UnitActionStack.Add(id, new ActionConfig());
            UnitActionStack[id].id = id;
        }
        ActionItem it = new ActionItem();
        it.text = "";
        it.pause_time = 0;
        it.type = type;//say = 1 pause = 2 skill = 3;crouch = 4, block=5
        it.param = idx;
        it.target = Vector3.zero;
        UnitActionStack[id].action.Add(it);
    }

    //为了增加，朝指定位置攻击功能加的
    void PushAction(int id, StackAction type, Vector3 position, int count)
    {
        if (!UnitActKey.Contains(id))
            UnitActKey.Add(id);
        if (!UnitActionStack.ContainsKey(id))
        {
            UnitActionStack.Add(id, new ActionConfig());
            UnitActionStack[id].id = id;
        }
        ActionItem it = new ActionItem();
        it.text = "";
        it.pause_time = 0;
        it.type = type;//say = 1 pause = 2 skill = 3;crouch = 4, block=5
        it.param = count;
        it.target = position;
        UnitActionStack[id].action.Add(it);
    }

    void PushAction(int id, StackAction type, float t = 0.0f, string text = "", int param = 0)
    {
        if (!UnitActKey.Contains(id))
            UnitActKey.Add(id);
        if (!UnitActionStack.ContainsKey(id))
        {
            UnitActionStack.Add(id, new ActionConfig());
            UnitActionStack[id].id = id;
        }
        ActionItem it = new ActionItem();
        it.text = text;
        it.pause_time = t;
        it.type = type;//say = 1 pause = 2 skill = 3;crouch = 4, block=5
        it.param = param;
        UnitActionStack[id].action.Add(it);
    }

    public void PushActionGuard(int id, int time)
    {
        PushAction(id, StackAction.GUARD, time, "", 0);
    }

    public void PushActionAggress(int id)
    {
        PushAction(id, StackAction.Aggress);
    }

    public void PushActionSkill(int id)
    {
        PushAction(id, StackAction.SKILL);
    }

    public void PushActionBlock(int id, int status)
    {
        PushAction(id, StackAction.BLOCK, 0, "", status);
    }

    public void PushActionUse(int id, int idx)
    {
        PushActionUse(id, StackAction.Use, idx);
    }
    public void PushActionCrouch(int id, int status)
    {
        PushAction(id, StackAction.CROUCH, 0, "", status);
    }

    public void PushActionPause(int id, float pause)
    {
        PushAction(id, StackAction.PAUSE, pause, "");
    }

    public void PushActionFollow(int id, int target)
    {
        PushAction(id, StackAction.Follow, 0, "", target);
    }

    public void PushActionWait(int id)
    {
        PushAction(id, StackAction.Wait);
    }

    public void PushActionFaceTo(int id, int target)
    {
        PushAction(id, StackAction.FaceTo, 0, "", target);
    }

    public void PushActionKill(int id, int target)
    {
        PushAction(id, StackAction.Kill, 0, "", target);
    }

    public void PushActionAttackTarget(int id, int targetIndex, int count = 0)
    {
        PushAction(id, StackAction.AttackTarget, LevelScriptBase.GetTarget(targetIndex), count);
    }

    public void StopAction(int id)
    {
        if (UnitActKey.Contains(id))
            UnitActKey.Remove(id);
        if (UnitActionStack.ContainsKey(id))
            UnitActionStack.Remove(id);
    }

#if !STRIP_DBG_SETTING
    public List<GameObject> wayPointList = new List<GameObject>();
    public List<GameObject> wayArrowList = new List<GameObject>();
    public void ShowWayPoint(bool on)
    {
        if (Global.Instance.GLevelItem == null)
            return;
        if (!on)
        {
            for (int i = 0; i < wayPointList.Count; i++)
            {
                 WsGlobal.RemoveLine(wayPointList[i]);
            }
            for (int i = 0; i < wayArrowList.Count; i++)
                GameObject.Destroy(wayArrowList[i]);
            wayArrowList.Clear();
            wayPointList.Clear();
        }

        if (on)
        {
            if (wayArrowList.Count != 0 || wayPointList.Count != 0)
                return;
            for (int i = 0; i < Global.Instance.GLevelItem.wayPoint.Count; i++)
            {
                GameObject obj = WsGlobal.AddDebugLine(Global.Instance.GLevelItem.wayPoint[i].pos - 2 * Vector3.up, Global.Instance.GLevelItem.wayPoint[i].pos + 2 * Vector3.up, Color.red, "WayPoint" + i, float.MaxValue, true);
                wayPointList.Add(obj);
                //BoxCollider capsule = obj.AddComponent<BoxCollider>();
                //capsule.isTrigger = true;
                //capsule.size = Vector3.one * (Global.GLevelItem.wayPoint[i].size) * 10;
                //capsule.center = Vector3.zero;
                obj.name = string.Format("WayPoint{0}", Global.Instance.GLevelItem.wayPoint[i].index);

                foreach (var each in Global.Instance.GLevelItem.wayPoint[i].link)
                {
                    GameObject objArrow = GameObject.Instantiate(Resources.Load("PathArrow")) as GameObject;
                    objArrow.transform.position = Global.Instance.GLevelItem.wayPoint[i].pos;
                    Vector3 vec = Global.Instance.GLevelItem.wayPoint[each.Key].pos - Global.Instance.GLevelItem.wayPoint[i].pos;
                    objArrow.transform.forward = vec.normalized;
                    objArrow.transform.localScale = new Vector3(30, 30, vec.magnitude / 2.2f);
                    objArrow.name = string.Format("Way{0}->Way{1}", Global.Instance.GLevelItem.wayPoint[each.Key].index, Global.Instance.GLevelItem.wayPoint[i].index);
                    wayArrowList.Add(objArrow);
                }
            }
        }

        GameData.Instance.gameStatus.ShowWayPoint = on;
    }
#endif
    public void OnSceneEvent(SceneEvent evt, int unit, GameObject trigger)
    {
        if (Scene_OnEvent != null)
            Scene_OnEvent.Invoke(Global.Instance.GScript, new object[] { trigger, unit, (int)evt});
    }
}

public enum StackAction
{
    SAY = 1,
    PAUSE = 2,
    SKILL = 3,
    CROUCH = 4,
    BLOCK = 5,
    GUARD = 6,
    Wait = 7,
    Follow = 8,
    Patrol = 9,
    FaceTo = 10,
    Kill = 11,
    Aggress = 12,
    AttackTarget = 13,
    Use = 14,//使用道具
}

//基本上只能存在单机模式下,联机不支持
public class ActionConfig
{
    public List<ActionItem> action = new List<ActionItem>();
    public int id;
    public void Update(float time)
    {
        if (action.Count != 0)
        {
            if (action[action.Count - 1].type == StackAction.PAUSE)
            {
                if (action[action.Count - 1].first)
                {
                    MeteorUnit unit = U3D.GetUnit(id);
                    if (unit != null)
                        unit.AIPause(true, action[action.Count - 1].pause_time);
                    action[action.Count - 1].first = false;
                }
                action[action.Count - 1].pause_time -= time;

                if (action[action.Count - 1].pause_time <= 0.0f)
                {
                    MeteorUnit unit = U3D.GetUnit(id);
                    if (unit != null)
                        unit.AIPause(false);
                    action.RemoveAt(action.Count - 1);
                }
            }
            else if (action[action.Count - 1].type == StackAction.SAY)
            {
                MeteorUnit unit = U3D.GetUnit(id);//, action[action.Count - 1])
                if (FightDialogState.Exist() && unit != null)
                    FightDialogState.Instance.InsertFightMessage(unit.name + " : " + action[action.Count - 1].text);
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.SKILL)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit && !unit.Dead)
                    unit.PlaySkill();
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.Aggress)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit && !unit.Dead)
                {
                    //在攻击或防御中,无法嘲讽其他
                    if (unit.posMng.IsAttackPose() || unit.posMng.IsHurtPose())
                    {

                    }
                    else
                        unit.posMng.ChangeAction(CommonAction.Taunt);
                }
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.CROUCH)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null)
                {
                    if (action[action.Count - 1].param == 1)
                    {
                        unit.controller.Input.OnKeyDownProxy(EKeyList.KL_Crouch, true);
                    }
                    else
                        unit.controller.Input.OnKeyUpProxy(EKeyList.KL_Crouch);
                }
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.BLOCK)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null)
                {
                    unit.controller.LockInput(action[action.Count - 1].param == 1);
                    if (!unit.Dead)
                        unit.posMng.LinkAction(0);
                }
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.GUARD)
            {
                if (action[action.Count - 1].pause_time > 0)
                {
                    MeteorUnit unit = U3D.GetUnit(id);
                    if (unit && !unit.Dead)
                        unit.Guard(true);
                    action[action.Count - 1].pause_time -= time;
                }
                else
                {
                    MeteorUnit unit = U3D.GetUnit(id);
                    if (unit && !unit.Dead)
                        unit.Guard(false);
                    action.RemoveAt(action.Count - 1);
                }
            }
            else if (action[action.Count - 1].type == StackAction.Wait)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null && unit.Robot != null)
                    unit.Robot.ChangeState(EAIStatus.Wait);
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.Follow)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null && unit.Robot != null)
                    unit.Robot.FollowTarget(action[action.Count - 1].param);
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.Patrol)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null && unit.Robot != null)
                {
                    unit.Robot.SetPatrolPath(action[action.Count - 1].Path);
                    unit.Robot.ChangeState(EAIStatus.Patrol);//多点巡逻.
                }
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.FaceTo)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                MeteorUnit target = U3D.GetUnit(action[action.Count - 1].param);
                if (unit != null && target != null)
                {
                    if (!unit.Dead)
                        unit.FaceToTarget(target);
                }
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.Kill)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                MeteorUnit target = U3D.GetUnit(action[action.Count - 1].param);
                if (unit != null && target != null)
                    unit.KillTarget(target);
                action.RemoveAt(action.Count - 1);
            }
            else if (action[action.Count - 1].type == StackAction.AttackTarget)
            {
                //攻击指定位置
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null && unit.Robot != null && !unit.Dead)
                {
                    if (unit.Robot.Status != EAIStatus.AttackTarget)
                    {
                        unit.Robot.AttackCount = action[action.Count - 1].param;
                        unit.Robot.AttackTarget = action[action.Count - 1].target;
                        unit.Robot.ChangeState(EAIStatus.AttackTarget);
                    }
                    //部分情况下，子状态不正确会导致很多奇怪的情况
                    if (unit.Robot.SubStatus != EAISubStatus.AttackGotoTarget && unit.Robot.SubStatus != EAISubStatus.AttackTarget && unit.Robot.SubStatus != EAISubStatus.AttackTargetSubRotateToTarget)
                        unit.Robot.SubStatus = EAISubStatus.AttackGotoTarget;
                    if (unit.Robot.OnAttackTarget())
                    {
                        action.RemoveAt(action.Count - 1);
                        unit.Robot.ChangeState(EAIStatus.Wait);
                    }
                }
            }
            else if (action[action.Count - 1].type == StackAction.Use)
            {
                MeteorUnit unit = U3D.GetUnit(id);
                if (unit != null)
                    unit.GetItem(action[action.Count - 1].param);
                action.RemoveAt(action.Count - 1);
            }
        }
    }
}
public class ActionItem
{
    public StackAction type;
    public bool first;
    public float pause_time;
    public string text;
    public int param;
    public Vector3 target;
    public List<int> Path;//for patrol
    public ActionItem()
    {
        first = true;
    }
}