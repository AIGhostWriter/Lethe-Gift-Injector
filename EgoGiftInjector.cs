// LetheGiftInjector (ver.2.4)
// - currentinfo → currentInfo 키 수정 (대소문자 오류)
// - Update payload: "data" → "saveInfo" (서버 422 오류 수정)
// - egs 키 불명 시 진단용 전체 구조 로그 출력
// - UpdateStateAsync null 방어 코드 강화

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace LetheGiftInjector
{
    [BepInPlugin("com.mod.lethegiftinjector", "LetheGiftInjector", "2.4.0")]
    public class LetheGiftInjectorPlugin : BasePlugin
    {
        internal static new ManualLogSource? Log;
        internal static string TokenFilePath =>
            Path.Combine(Paths.ConfigPath, "LetheGiftInjector_token.txt");

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("LetheGiftInjector v2.4 로드");
            AddComponent<InjectorUI>();
        }
    }

    public class InjectorUI : MonoBehaviour
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string FETCH_URL  = "https://api.lethelc.site/dashboard/md/get";
        private const string UPDATE_URL = "https://api.lethelc.site/dashboard/md/update";

        private bool      _showPanel   = false;
        private string    _token       = "";
        private string    _status      = "";
        private JsonNode? _state       = null;
        private bool      _fetched     = false;
        private Task?     _pendingTask = null;

        // Fetch 후 확인된 실제 egs 키 이름 (진단 후 채워짐)
        private string    _egsKey      = "egs";

        private string _inputId   = "";
        private string _inputTier = "2";

        private int _autoPage = 0;
        private int _giftPage = 0;
        private const int PAGE_SIZE = 8;

        private Rect    _windowRect  = new Rect(20, 20, 420, 600);
        private bool    _isDragging  = false;
        private Vector2 _dragOffset  = Vector2.zero;

        private System.Collections.Generic.List<(int id, int ul)> _pendingGifts
            = new System.Collections.Generic.List<(int id, int ul)>();

        private readonly string[] _keywords = {
            "All","Combustion","Laceration","Vibration","Burst",
            "Sinking","Breath","Charge","Slash","Penetrate","Hit","None"
        };
        private int _kwIndex = 0;

        private readonly (int gid, string kw)[] _allGifts = {
            (9001,"Combustion"),(9002,"None"),(9003,"Combustion"),(9004,"None"),
            (9005,"Laceration"),(9006,"None"),(9007,"None"),(9008,"Laceration"),
            (9009,"Combustion"),(9010,"None"),(9011,"None"),(9012,"Hit"),
            (9013,"Burst"),(9014,"None"),(9015,"Vibration"),(9016,"Vibration"),
            (9017,"None"),(9018,"Burst"),(9019,"Penetrate"),(9020,"Laceration"),
            (9021,"None"),(9022,"None"),(9023,"Burst"),(9024,"Vibration"),
            (9025,"None"),(9026,"None"),(9027,"Hit"),(9028,"None"),
            (9029,"Laceration"),(9030,"Penetrate"),(9031,"Vibration"),(9032,"Slash"),
            (9033,"Burst"),(9034,"Combustion"),(9035,"None"),(9036,"None"),
            (9037,"None"),(9038,"None"),(9039,"None"),(9040,"None"),
            (9041,"Sinking"),(9042,"Laceration"),(9043,"Charge"),(9044,"Vibration"),
            (9045,"Combustion"),(9046,"Breath"),(9047,"Burst"),(9048,"Laceration"),
            (9049,"Sinking"),(9050,"Laceration"),(9051,"Breath"),(9052,"Charge"),
            (9053,"Combustion"),(9054,"Sinking"),(9055,"Vibration"),(9056,"Breath"),
            (9057,"Charge"),(9058,"None"),(9059,"Sinking"),(9060,"Burst"),
            (9061,"Sinking"),(9062,"Charge"),(9063,"Breath"),(9064,"Burst"),
            (9065,"Sinking"),(9066,"Breath"),(9067,"None"),(9068,"None"),
            (9069,"Charge"),(9070,"Breath"),(9071,"Combustion"),(9072,"Charge"),
            (9073,"Breath"),(9074,"Sinking"),(9075,"Charge"),(9076,"None"),
            (9077,"None"),(9078,"None"),(9079,"None"),(9080,"None"),
            (9081,"None"),(9082,"None"),(9083,"None"),(9084,"None"),(9085,"None"),
            (9086,"Vibration"),(9087,"Combustion"),(9088,"Combustion"),
            (9089,"Laceration"),(9090,"Laceration"),(9091,"Vibration"),(9092,"Vibration"),
            (9093,"Burst"),(9094,"Burst"),(9095,"Sinking"),(9096,"Sinking"),
            (9097,"Breath"),(9098,"Breath"),(9099,"Charge"),(9100,"Charge"),
            (9101,"Combustion"),(9102,"Combustion"),(9103,"Combustion"),(9104,"Combustion"),(9105,"Combustion"),
            (9106,"Laceration"),(9107,"Laceration"),(9108,"Laceration"),(9109,"Laceration"),(9110,"Laceration"),
            (9111,"Vibration"),(9112,"Vibration"),(9113,"Vibration"),(9114,"Vibration"),(9115,"Vibration"),(9116,"Vibration"),
            (9117,"Burst"),(9118,"Burst"),(9119,"Burst"),(9120,"Burst"),(9121,"Burst"),
            (9122,"Sinking"),(9123,"Sinking"),(9124,"Sinking"),(9125,"Sinking"),(9126,"Sinking"),
            (9127,"Breath"),(9128,"Breath"),(9129,"Breath"),(9130,"Breath"),(9131,"Breath"),
            (9132,"Charge"),(9133,"Charge"),(9134,"Charge"),(9135,"Charge"),(9136,"Charge"),
            (9137,"Slash"),(9138,"Slash"),(9139,"Slash"),(9140,"Slash"),(9141,"Slash"),(9142,"Slash"),
            (9143,"Penetrate"),(9144,"Penetrate"),(9145,"Penetrate"),(9146,"Penetrate"),(9147,"Penetrate"),
            (9148,"Hit"),(9149,"Hit"),(9150,"Hit"),(9151,"Hit"),(9152,"Hit"),
            (9153,"None"),(9154,"None"),
        };

        public InjectorUI(IntPtr ptr) : base(ptr) { }

        private void Start()
        {
            try
            {
                if (File.Exists(LetheGiftInjectorPlugin.TokenFilePath))
                {
                    _token = File.ReadAllText(LetheGiftInjectorPlugin.TokenFilePath).Trim();
                    _status = "저장된 토큰을 로드했다. Fetch State를 눌러라.";
                    LetheGiftInjectorPlugin.Log?.LogInfo("저장된 토큰 로드 완료");
                }
            }
            catch (Exception ex)
            {
                LetheGiftInjectorPlugin.Log?.LogWarning("토큰 로드 실패: " + ex.Message);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                _showPanel = !_showPanel;

            if (_pendingTask != null && _pendingTask.IsCompleted)
            {
                if (_pendingTask.IsFaulted)
                    _status = "오류: " + (_pendingTask.Exception?.InnerException?.Message ?? "알 수 없음");
                _pendingTask = null;
            }
        }

        private void OnGUI()
        {
            if (!_showPanel) return;

            GUI.Box(_windowRect, "Lethe Gift Injector ver.2.4");

            var titleBar = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 20);
            var e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragOffset = new Vector2(_windowRect.x - e.mousePosition.x,
                                          _windowRect.y - e.mousePosition.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                _windowRect.x = e.mousePosition.x + _dragOffset.x;
                _windowRect.y = e.mousePosition.y + _dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
            }

            GUILayout.BeginArea(new Rect(
                _windowRect.x + 5,
                _windowRect.y + 22,
                _windowRect.width - 10,
                _windowRect.height - 27));

            GUILayout.Label("Token:");
            GUILayout.BeginHorizontal();
            _token = GUILayout.TextField(_token, GUILayout.Width(310));
            if (GUILayout.Button("저장", GUILayout.Width(45))) SaveToken();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            bool isBusy = _pendingTask != null;
            GUILayout.BeginHorizontal();
            GUI.enabled = !isBusy && !string.IsNullOrEmpty(_token);
            if (GUILayout.Button("① Fetch State", GUILayout.Width(130)))
                _pendingTask = FetchStateAsync();
            GUI.enabled = !isBusy && _fetched && _pendingGifts.Count > 0;
            if (GUILayout.Button("② Update State", GUILayout.Width(130)))
                _pendingTask = UpdateStateAsync();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(isBusy ? "[처리 중...]" : _status);

            GUILayout.Space(6);

            GUILayout.Label($"주입 대기 목록 ({_pendingGifts.Count}개):");
            int autoStart = _autoPage * PAGE_SIZE;
            for (int i = autoStart; i < Math.Min(autoStart + PAGE_SIZE, _pendingGifts.Count); i++)
            {
                var (pid, pul) = _pendingGifts[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"ID:{pid}  Tier:{pul}", GUILayout.Width(260));
                if (GUILayout.Button("X", GUILayout.Width(25)))
                { _pendingGifts.RemoveAt(i); _autoPage = 0; }
                GUILayout.EndHorizontal();
            }
            if (_pendingGifts.Count == 0) GUILayout.Label("[비어 있음]");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30)) && _autoPage > 0) _autoPage--;
            GUILayout.Label(
                $"{_autoPage+1}/{Math.Max(1,(_pendingGifts.Count+PAGE_SIZE-1)/PAGE_SIZE)}",
                GUILayout.Width(50));
            if (GUILayout.Button(">", GUILayout.Width(30)) &&
                (autoStart+PAGE_SIZE) < _pendingGifts.Count) _autoPage++;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("ID:", GUILayout.Width(20));
            _inputId   = GUILayout.TextField(_inputId,   GUILayout.Width(70));
            GUILayout.Label("Tier:", GUILayout.Width(30));
            _inputTier = GUILayout.TextField(_inputTier, GUILayout.Width(25));
            if (GUILayout.Button("추가", GUILayout.Width(50))) TryAdd();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.Label("기프트 목록 (기본 Tier=2):");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _keywords.Length; i++)
                if (GUILayout.Toggle(_kwIndex == i, _keywords[i], "Button", GUILayout.Width(54)))
                { _kwIndex = i; _giftPage = 0; }
            GUILayout.EndHorizontal();

            string selKw = _keywords[_kwIndex];
            var filtered = new System.Collections.Generic.List<(int gid, string kw)>();
            foreach (var g in _allGifts)
                if (selKw == "All" || g.kw == selKw) filtered.Add(g);

            int giftStart = _giftPage * PAGE_SIZE;
            for (int i = giftStart; i < Math.Min(giftStart + PAGE_SIZE, filtered.Count); i++)
            {
                var (gid, kw) = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{gid} [{kw}]", GUILayout.Width(260));
                if (GUILayout.Button("+", GUILayout.Width(25))) AddToList(gid, 2);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30)) && _giftPage > 0) _giftPage--;
            GUILayout.Label(
                $"{_giftPage+1}/{Math.Max(1,(filtered.Count+PAGE_SIZE-1)/PAGE_SIZE)}",
                GUILayout.Width(50));
            if (GUILayout.Button(">", GUILayout.Width(30)) &&
                (giftStart+PAGE_SIZE) < filtered.Count) _giftPage++;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void SaveToken()
        {
            try
            {
                File.WriteAllText(LetheGiftInjectorPlugin.TokenFilePath, _token.Trim());
                _status = "토큰 저장 완료";
            }
            catch (Exception ex)
            {
                _status = "토큰 저장 실패: " + ex.Message;
            }
        }

        private async Task FetchStateAsync()
        {
            _status = "Fetch 중...";
            _fetched = false;
            try
            {
                var reqBody  = $"{{\"token\":\"{EscapeJson(_token.Trim())}\"}}";
                var content  = new StringContent(reqBody, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(FETCH_URL, content);
                var respStr  = await response.Content.ReadAsStringAsync();

                // 전체 응답을 로그에 출력 (구조 파악용)
                LetheGiftInjectorPlugin.Log?.LogInfo(
                    $"Fetch 응답 전체 ({(int)response.StatusCode}): {respStr}");

                if (!response.IsSuccessStatusCode)
                {
                    _status = $"Fetch 실패 ({(int)response.StatusCode}) — 로그 확인";
                    return;
                }

                _state = JsonNode.Parse(respStr);

                // currentInfo 키 존재 여부 확인 (대소문자 주의)
                var currentInfo = _state?["currentInfo"];
                if (currentInfo == null)
                {
                    _status = "[오류] 응답에 'currentInfo' 없음 — 로그에서 키 이름 확인";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "FetchStateAsync: currentInfo is null. 최상위 키 목록 확인 필요.");
                    return;
                }

                // egs 또는 다른 이름의 기프트 배열 탐색
                // 로그에서 확인된 키 이름으로 교체할 것
                var egsNode = currentInfo["egs"];
                if (egsNode == null)
                {
                    // 키를 찾지 못한 경우 currentInfo 하위 키 목록을 전부 출력
                    LetheGiftInjectorPlugin.Log?.LogWarning(
                        "FetchStateAsync: 'egs' 키 없음. currentInfo 구조를 확인하라: "
                        + currentInfo.ToJsonString());
                    _status = "[경고] egs 키 없음 — 로그에서 실제 키 이름 확인 후 개발자에게 제보";
                    // egs가 없어도 나머지 처리는 계속 (빈 상태로 취급)
                    _fetched = true;
                    return;
                }

                _egsKey  = "egs";
                _fetched = true;
                int egsCount = egsNode.AsArray().Count;
                _status = $"Fetch 성공. 현재 기프트: {egsCount}개";
            }
            catch (Exception ex)
            {
                _status = "Fetch 오류: " + ex.Message;
                LetheGiftInjectorPlugin.Log?.LogError("Fetch 오류: " + ex);
            }
        }

        private async Task UpdateStateAsync()
        {
            if (_state == null) { _status = "[오류] state 없음. Fetch 먼저 실행"; return; }
            _status = "Update 중...";
            try
            {
                var currentInfo = _state["currentInfo"];
                if (currentInfo == null)
                {
                    _status = "[오류] currentInfo 없음 — 로그 확인";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "UpdateStateAsync: currentInfo is null. state: " + _state.ToJsonString());
                    return;
                }

                var egsNode = currentInfo[_egsKey];
                if (egsNode == null)
                {
                    _status = $"[오류] '{_egsKey}' 키 없음 — 로그 확인";
                    LetheGiftInjectorPlugin.Log?.LogError(
                        "UpdateStateAsync: egs node is null. currentInfo: " + currentInfo.ToJsonString());
                    return;
                }

                var egs = egsNode.AsArray();

                int added = 0;
                foreach (var (gid, gul) in _pendingGifts)
                {
                    bool exists = false;
                    foreach (var eg in egs)
                        if (eg?["id"]?.GetValue<int>() == gid) { exists = true; break; }
                    if (exists) continue;

                    egs.Add(new JsonObject
                    {
                        ["id"]   = gid,
                        ["pids"] = new JsonArray(),
                        ["un"]   = 0,
                        ["ul"]   = gul
                    });
                    added++;
                }

                var stateJson = _state.ToJsonString();
                var payload = new JsonObject
                {
                    ["token"]    = _token.Trim(),
                    ["saveInfo"] = JsonNode.Parse(stateJson)
                };

                var reqBody  = payload.ToJsonString();
                var content  = new StringContent(reqBody, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(UPDATE_URL, content);
                var respStr  = await response.Content.ReadAsStringAsync();

                LetheGiftInjectorPlugin.Log?.LogInfo(
                    $"Update 응답 ({(int)response.StatusCode}): {respStr}");

                if (!response.IsSuccessStatusCode)
                {
                    _status = $"Update 실패 ({(int)response.StatusCode}) — 로그 확인";
                    return;
                }

                _status = $"Update 성공. {added}개 추가됨. 던전 입장 시 적용.";
                _pendingGifts.Clear();
                _autoPage = 0;
            }
            catch (Exception ex)
            {
                _status = "Update 오류: " + ex.Message;
                LetheGiftInjectorPlugin.Log?.LogError("Update 오류: " + ex);
            }
        }

        private void TryAdd()
        {
            if (!int.TryParse(_inputId, out int id))
            { _status = "[오류] 유효한 숫자 아님"; return; }
            if (!int.TryParse(_inputTier, out int tier) || tier < 0 || tier > 2) tier = 2;
            AddToList(id, tier);
        }

        private void AddToList(int id, int ul)
        {
            foreach (var (eid, _) in _pendingGifts)
                if (eid == id) { _status = $"ID {id} 이미 목록에 있음"; return; }
            _pendingGifts.Add((id, ul));
            _status = $"ID {id} (Tier {ul}) 추가됨";
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}