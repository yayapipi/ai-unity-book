using System;
using UnityEngine;
using XLua;

public class LuaDemo : MonoBehaviour
{
    [Header("Lua Code (return a table with optional Start/Update/OnDestroy)")]
    [TextArea(8, 30)]
    public string luaCode = @"
local M = {}

function M:Start()
    print('Lua Start: ' .. self.go.name)
end

function M:Update(dt)
end

return M
";

    public bool runOnStart = true;

    private static LuaEnv _env;
    private LuaTable _self;

    private LuaFunction _luaStart;
    private LuaFunction _luaUpdate;
    private LuaFunction _luaOnDestroy;

    void Awake()
    {
        if (_env == null) _env = new LuaEnv();
        if (runOnStart) LoadAndBind();
    }

    void Start()
    {
        SafeCall(() => _luaStart?.Call(_self));
    }

    void Update()
    {
        SafeCall(() => _luaUpdate?.Call(_self, Time.deltaTime));
    }

    void OnDestroy()
    {
        SafeCall(() => _luaOnDestroy?.Call(_self));
        DisposeLuaRefs();
    }

    [ContextMenu("Reload Lua (Inspector Code)")]
    public void ReloadLua()
    {
        LoadAndBind();
        SafeCall(() => _luaStart?.Call(_self));
    }

    private void LoadAndBind()
    {
        DisposeLuaRefs();

        if (string.IsNullOrWhiteSpace(luaCode))
        {
            Debug.LogWarning("[LuaDemo] luaCode is empty.");
            return;
        }

        object[] ret;
        try
        {
            ret = _env.DoString(luaCode, "LuaDemo_InspectorChunk");
        }
        catch (Exception e)
        {
            Debug.LogError("[LuaDemo] Lua compile/runtime error:\n" + e);
            return;
        }

        _self = (ret != null && ret.Length > 0) ? ret[0] as LuaTable : null;
        if (_self == null)
        {
            Debug.LogError("[LuaDemo] Lua code must `return` a table (e.g. return M).");
            return;
        }

        _self.Set("go", gameObject);
        _self.Set("transform", transform);

        _luaStart = _self.Get<LuaFunction>("Start");
        _luaUpdate = _self.Get<LuaFunction>("Update");
        _luaOnDestroy = _self.Get<LuaFunction>("OnDestroy");
    }

    private void DisposeLuaRefs()
    {
        _luaStart?.Dispose(); _luaStart = null;
        _luaUpdate?.Dispose(); _luaUpdate = null;
        _luaOnDestroy?.Dispose(); _luaOnDestroy = null;

        _self?.Dispose(); _self = null;
    }

    private void SafeCall(Action act)
    {
        try { act?.Invoke(); }
        catch (Exception e)
        {
            Debug.LogError("[LuaDemo] Lua call error:\n" + e);
        }
    }
}
