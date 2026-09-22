//+------------------------------------------------------------------+
//|                                      NTBot_OperationalZones.mq5 |
//|                        Copyright 2026, NTBot Team                  |
//|     Draws TI operational zones on the MT5 chart (XAUUSD-first).  |
//|     Dual feed: WebRequest /api/mt5/zones + Common Files fallback.|
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, NTBot Team"
#property link      "https://github.com/ntbot/ntbot"
#property version   "1.33"
#property strict
#property indicator_chart_window
#property indicator_plots 0

//--- Inputs
input string InpApiBaseUrl      = "http://127.0.0.1:5053"; // NTBot API base (trim; no trailing /)
input string InpApiKey          = "";                      // Optional Bearer token
input string InpSymbolOverride  = "";                      // Empty = chart Symbol()
input string InpTimeframeKey    = "60";                    // TI timeframe key (5/15/30/60/240/1440)
input int    InpMaxZones        = 6;                       // Max rectangles (XAUUSD default 6)
input int    InpRefreshSec      = 30;                      // Poll interval (seconds)
input int    InpLookbackBars    = 120;                     // Rectangle left width in bars
input int    InpHttpTimeoutMs   = 25000;                   // WebRequest timeout (cold TI ~12s)
input bool   InpTryAltHost      = true;                    // On 4014, retry localhost↔127.0.0.1
input bool   InpUseFileFallback = true;                    // Read Common Files if WebRequest fails
input string InpZonesFileName   = "NTBot_zones_{symbol}.txt"; // FILE_COMMON name pattern
input bool   InpPreferFile      = false;                   // true = file first (skip HTTP)
input bool   InpDrawLabels      = true;                    // Text labels on right edge
input bool   InpShowLiquidity   = true;                    // Include Liq ↑/↓ pools
input bool   InpShowComment     = true;                    // Chart Comment status line
input bool   InpDebugLog        = false;                   // Print parse/draw diagnostics

#define NTBOT_OZ_PREFIX "NTBOT_OZ_"

datetime g_lastFetch = 0;
string   g_lastDelim = "";
int      g_zoneCount = 0;
int      g_lastHttp  = 0;
int      g_lastErr   = 0;
string   g_status    = "init";
string   g_logical   = "";
string   g_lastUrl   = "";
bool     g_alertedAllowlist = false;

//+------------------------------------------------------------------+
int OnInit()
{
   IndicatorSetString(INDICATOR_SHORTNAME, "NTBot Zones");
   EventSetTimer(MathMax(5, InpRefreshSec));
   UpdateStatus("init", 0, 0, "starting…");
   if(!RefreshZones(true))
      Print("NTBot_OperationalZones: first fetch failed — WebRequest allowlist + file bridge. base=",
            NormalizeBaseUrl(InpApiBaseUrl), " lastUrl=", g_lastUrl, " err=", g_lastErr);
   return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
   EventKillTimer();
   DeleteNtbotZoneObjects();
   Comment("");
}

//+------------------------------------------------------------------+
int OnCalculate(const int rates_total,
                const int prev_calculated,
                const datetime &time[],
                const double &open[],
                const double &high[],
                const double &low[],
                const double &close[],
                const long &tick_volume[],
                const long &volume[],
                const int &spread[])
{
   return rates_total;
}

//+------------------------------------------------------------------+
void OnTimer()
{
   RefreshZones(false);
}

//+------------------------------------------------------------------+
string NormalizeBaseUrl(const string raw)
{
   string u = raw;
   StringTrimLeft(u);
   StringTrimRight(u);
   // Strip invisible BOM / zero-width that break allowlist matching.
   while(StringLen(u) > 0)
   {
      ushort c0 = StringGetCharacter(u, 0);
      if(c0 == 0xFEFF || c0 == 0x200B || c0 <= 32)
         u = StringSubstr(u, 1);
      else
         break;
   }
   while(StringLen(u) > 0 && StringGetCharacter(u, StringLen(u) - 1) == '/')
      u = StringSubstr(u, 0, StringLen(u) - 1);
   return u;
}

//+------------------------------------------------------------------+
string SwapLoopbackHost(const string baseUrl)
{
   // http://127.0.0.1:5053 ↔ http://localhost:5053
   string u = baseUrl;
   string needle = "://127.0.0.1";
   int p = StringFind(u, needle);
   if(p >= 0)
   {
      StringReplace(u, "://127.0.0.1", "://localhost");
      return u;
   }
   needle = "://localhost";
   p = StringFind(u, needle);
   if(p >= 0)
   {
      StringReplace(u, "://localhost", "://127.0.0.1");
      return u;
   }
   return "";
}

//+------------------------------------------------------------------+
string BuildZonesUrl(const string baseUrl, const string logical)
{
   return baseUrl + "/api/mt5/zones?symbol=" + logical
          + "&timeframe=" + InpTimeframeKey
          + "&max=" + IntegerToString(MathMax(1, InpMaxZones));
}

//+------------------------------------------------------------------+
string ResolveZonesFileName(const string logical)
{
   string name = InpZonesFileName;
   StringReplace(name, "{symbol}", logical);
   StringReplace(name, "{SYMBOL}", logical);
   return name;
}

//+------------------------------------------------------------------+
void UpdateStatus(const string state, const int http, const int err, const string detail)
{
   g_status = state;
   g_lastHttp = http;
   g_lastErr = err;
   if(!InpShowComment)
      return;

   string urlHint = (g_lastUrl == "" ? "" : " | " + g_lastUrl);
   string line = StringFormat(
      "NTBot Zones | %s | HTTP %d | zones %d | err %d | %s | %s%s",
      (g_logical == "" ? "?" : g_logical),
      http,
      g_zoneCount,
      err,
      state,
      detail,
      urlHint);
   // Keep Comment readable on chart (MT5 truncates very long comments).
   if(StringLen(line) > 220)
      line = StringSubstr(line, 0, 217) + "...";
   Comment(line);
}

//+------------------------------------------------------------------+
bool ApplyDelimPayload(const string delim, const int httpCode, const int apiCount, const string sourceTag)
{
   if(delim == "")
   {
      if(apiCount == 0)
      {
         DeleteNtbotZoneObjects();
         g_zoneCount = 0;
         g_lastDelim = "";
         ChartRedraw(0);
         UpdateStatus("ok_empty_" + sourceTag, httpCode, 0, "0 zones via " + sourceTag);
         return true;
      }
      UpdateStatus("parse_fail", httpCode, 0, "empty delim via " + sourceTag);
      return false;
   }

   if(delim == g_lastDelim)
   {
      UpdateStatus("ok_cached_" + sourceTag, httpCode, 0,
                   IntegerToString(g_zoneCount) + " zones (unchanged/" + sourceTag + ")");
      return true;
   }

   g_lastDelim = delim;
   DeleteNtbotZoneObjects();
   g_zoneCount = DrawDelimZones(delim);
   ChartRedraw(0);
   UpdateStatus("ok_" + sourceTag, httpCode, 0,
                IntegerToString(g_zoneCount) + " drawn via " + sourceTag);
   if(InpDebugLog || g_zoneCount == 0)
      Print("NTBot zones: drew ", g_zoneCount, " via ", sourceTag, " for ", g_logical);
   return true;
}

//+------------------------------------------------------------------+
bool TryWebRequestOnce(const string url, string &outBody, int &outCode, int &outErr)
{
   outBody = "";
   outCode = -1;
   outErr = 0;
   g_lastUrl = url;

   string headers = "Content-Type: application/json\r\n";
   if(InpApiKey != "")
      headers += "Authorization: Bearer " + InpApiKey + "\r\n";

   char   post[];
   char   result[];
   string result_headers;
   ArrayResize(post, 0);

   ResetLastError();
   int timeout = MathMax(5000, InpHttpTimeoutMs);
   outCode = WebRequest("GET", url, headers, timeout, post, result, result_headers);
   if(outCode == -1)
   {
      outErr = GetLastError();
      return false;
   }

   outBody = CharArrayToString(result, 0, WHOLE_ARRAY, CP_UTF8);
   return true;
}

//+------------------------------------------------------------------+
bool FetchViaWebRequest(const string logical, string &outDelim, int &outHttp, int &outApiCount)
{
   outDelim = "";
   outHttp = -1;
   outApiCount = -1;

   string baseUrl = NormalizeBaseUrl(InpApiBaseUrl);
   string url = BuildZonesUrl(baseUrl, logical);

   string body;
   int code = -1;
   int err = 0;
   bool ok = TryWebRequestOnce(url, body, code, err);

   if(!ok && err == 4014 && InpTryAltHost)
   {
      string altBase = SwapLoopbackHost(baseUrl);
      if(altBase != "" && altBase != baseUrl)
      {
         string altUrl = BuildZonesUrl(altBase, logical);
         Print("NTBot zones: 4014 on ", url, " — retrying ", altUrl);
         ok = TryWebRequestOnce(altUrl, body, code, err);
      }
   }

   g_lastErr = err;
   outHttp = code;

   if(!ok)
   {
      string hint = "WebRequest failed";
      if(err == 4014)
      {
         hint = "URL not allowed — add BOTH http://127.0.0.1:5053 AND http://localhost:5053; OK→restart MT5; exact URL=" + g_lastUrl;
         if(!g_alertedAllowlist)
         {
            Alert("NTBot Zones: WebRequest 4014. Add allowlist + restart, or use file bridge (Common\\Files\\",
                  ResolveZonesFileName(logical), ")");
            g_alertedAllowlist = true;
         }
      }
      else if(err == 4060)
         hint = "WebRequest disabled in terminal options";
      else if(err == 5200 || err == 5203)
         hint = "cannot connect — is NtBot.Api running? url=" + g_lastUrl;

      Print("NTBot zones WebRequest failed err=", err, " url=", g_lastUrl, " — ", hint);
      UpdateStatus("webrequest_fail", -1, err, hint);
      return false;
   }

   if(code != 200)
   {
      string preview = StringSubstr(body, 0, MathMin(120, StringLen(body)));
      Print("NTBot zones HTTP=", code, " body=", preview, " url=", g_lastUrl);
      UpdateStatus("http_error", code, 0, preview);
      return false;
   }

   outDelim = ExtractJsonStringField(body, "delim");
   if(outDelim == "")
   {
      outDelim = ExtractJsonStringFieldRaw(body, "delim");
      StringReplace(outDelim, "\\n", "\n");
      StringReplace(outDelim, "\\\"", "\"");
   }
   outApiCount = (int)ExtractJsonIntField(body, "count");
   return true;
}

//+------------------------------------------------------------------+
bool FetchViaFile(const string logical, string &outDelim, int &outApiCount)
{
   outDelim = "";
   outApiCount = -1;

   string fileName = ResolveZonesFileName(logical);
   g_lastUrl = "FILE_COMMON:" + fileName;

   // Prefer Common Files (API default). Also try terminal MQL5/Files.
   int handle = FileOpen(fileName, FILE_READ | FILE_TXT | FILE_ANSI | FILE_COMMON | FILE_SHARE_READ);
   if(handle == INVALID_HANDLE)
      handle = FileOpen(fileName, FILE_READ | FILE_TXT | FILE_ANSI | FILE_SHARE_READ);

   if(handle == INVALID_HANDLE)
   {
      int err = GetLastError();
      g_lastErr = err;
      if(InpDebugLog)
         Print("NTBot zones file open failed name=", fileName, " err=", err);
      return false;
   }

   string content = "";
   while(!FileIsEnding(handle))
   {
      string line = FileReadString(handle);
      if(line == "" && FileIsEnding(handle))
         break;
      StringTrimLeft(line);
      StringTrimRight(line);
      if(line == "" || StringGetCharacter(line, 0) == '#')
         continue;
      if(content != "")
         content += "\n";
      content += line;
   }
   FileClose(handle);

   if(content == "")
   {
      outApiCount = 0;
      outDelim = "";
      return true;
   }

   outDelim = content;
   // Count non-empty lines as apiCount approximation.
   string parts[];
   outApiCount = StringSplit(content, '\n', parts);
   return true;
}

//+------------------------------------------------------------------+
bool RefreshZones(const bool force)
{
   if(!force && TimeCurrent() - g_lastFetch < InpRefreshSec)
      return true;

   string sym = (InpSymbolOverride == "" ? Symbol() : InpSymbolOverride);
   string logical = MapToLogicalSymbol(sym);
   g_logical = logical;

   string delim = "";
   int http = -1;
   int apiCount = -1;
   bool got = false;
   string source = "";

   if(InpPreferFile && InpUseFileFallback)
   {
      if(FetchViaFile(logical, delim, apiCount))
      {
         got = true;
         source = "file";
         http = 0;
      }
   }

   if(!got)
   {
      if(FetchViaWebRequest(logical, delim, http, apiCount))
      {
         got = true;
         source = "http";
      }
      else if(InpUseFileFallback)
      {
         Print("NTBot zones: WebRequest failed — trying FILE_COMMON ", ResolveZonesFileName(logical));
         if(FetchViaFile(logical, delim, apiCount))
         {
            got = true;
            source = "file";
            http = 0;
            g_lastErr = 0;
         }
      }
   }

   if(!got)
      return false;

   g_lastFetch = TimeCurrent();
   return ApplyDelimPayload(delim, http, apiCount, source);
}

//+------------------------------------------------------------------+
string MapToLogicalSymbol(const string mt5Symbol)
{
   string s = mt5Symbol;
   StringToUpper(s);
   // Broker suffixes: XAUUSD.a, XAUUSDm, GOLD, etc.
   if(StringFind(s, "XAU") >= 0 || StringFind(s, "GOLD") >= 0)
      return "XAUUSD";
   if(StringFind(s, "WIN") >= 0)
      return "WIN";
   if(StringFind(s, "WDO") >= 0)
      return "WDO";
   // Strip common suffixes: .a .i .m / # micro suffix letters
   int cut = StringLen(s);
   for(int i = 0; i < StringLen(s); i++)
   {
      ushort ch = StringGetCharacter(s, i);
      if(ch == '.' || ch == '#' || ch == '-' || ch == '_')
      {
         cut = i;
         break;
      }
   }
   if(cut > 0 && cut < StringLen(s))
      s = StringSubstr(s, 0, cut);
   // Trailing broker letter(s): EURUSDm, XAUUSDpro → keep alphabetic core until digits end
   while(StringLen(s) > 3)
   {
      ushort last = StringGetCharacter(s, StringLen(s) - 1);
      if(last >= '0' && last <= '9')
         break;
      // strip single trailing lowercase-ish broker tag only if base looks like XXXYYY
      if(StringLen(s) > 6 && (last == 'M' || last == 'I' || last == 'A' || last == 'C'))
      {
         s = StringSubstr(s, 0, StringLen(s) - 1);
         continue;
      }
      break;
   }
   return s;
}

//+------------------------------------------------------------------+
int DrawDelimZones(const string delim)
{
   string lines[];
   int n = StringSplit(delim, '\n', lines);
   if(n <= 0)
      return 0;

   datetime tRight = iTime(Symbol(), Period(), 0);
   datetime tLeft  = iTime(Symbol(), Period(), MathMin(InpLookbackBars, Bars(Symbol(), Period()) - 1));
   if(tLeft == 0) tLeft = tRight - PeriodSeconds() * InpLookbackBars;

   int drawn = 0;
   for(int i = 0; i < n; i++)
   {
      string line = lines[i];
      StringTrimLeft(line);
      StringTrimRight(line);
      if(line == "" || StringGetCharacter(line, 0) == '#')
         continue;

      string p[];
      int pc = StringSplit(line, '|', p);
      if(pc < 9)
         continue;

      string id    = p[0];
      string kind  = p[1];
      string side  = p[2];
      double lo    = StringToDouble(p[3]);
      double hi    = StringToDouble(p[4]);
      string rgb   = p[5];
      int    alpha = (int)StringToInteger(p[6]);
      string style = p[7];
      string label = LocalizeLabel(SanitizeLabel(p[8]), kind, side);

      if(!InpShowLiquidity && kind == "liquidity")
         continue;
      if(!(lo > 0 || hi > 0))
         continue;

      double top = MathMax(lo, hi);
      double bot = MathMin(lo, hi);
      color  clr = SoftPaletteColor(kind, side, RgbStringToColor(rgb));
      // Light fills: visible green (buy) / red (sell) without hiding candles.
      int softAlpha = MathMin(48, MathMax(28, (int)(alpha * 0.85 + 18)));

      string rectName = NTBOT_OZ_PREFIX + "R_" + id;
      if(!ObjectCreate(0, rectName, OBJ_RECTANGLE, 0, tLeft, top, tRight, bot))
      {
         ObjectMove(0, rectName, 0, tLeft, top);
         ObjectMove(0, rectName, 1, tRight, bot);
      }
      ObjectSetInteger(0, rectName, OBJPROP_COLOR, SoftFillColor(clr, softAlpha));
      ObjectSetInteger(0, rectName, OBJPROP_STYLE, style == "dash" ? STYLE_DASH : STYLE_SOLID);
      ObjectSetInteger(0, rectName, OBJPROP_WIDTH, 1);
      ObjectSetInteger(0, rectName, OBJPROP_BACK, true);
      ObjectSetInteger(0, rectName, OBJPROP_FILL, true);
      ObjectSetInteger(0, rectName, OBJPROP_SELECTABLE, false);
      ObjectSetInteger(0, rectName, OBJPROP_HIDDEN, true);

      // Soft edge guides (visible but not harsh).
      color edge = SoftEdgeColor(clr);
      string h1 = NTBOT_OZ_PREFIX + "H1_" + id;
      string h2 = NTBOT_OZ_PREFIX + "H2_" + id;
      CreateHLine(h1, bot, edge, style == "dash" ? STYLE_DASH : STYLE_SOLID, 1);
      if(top != bot)
         CreateHLine(h2, top, edge, style == "dash" ? STYLE_DASH : STYLE_DOT, 1);

      if(InpDrawLabels)
      {
         string lab = NTBOT_OZ_PREFIX + "L_" + id;
         if(!ObjectCreate(0, lab, OBJ_TEXT, 0, tRight, top))
            ObjectMove(0, lab, 0, tRight, top);
         ObjectSetString(0, lab, OBJPROP_TEXT, " " + label);
         ObjectSetInteger(0, lab, OBJPROP_COLOR, SoftEdgeColor(clr));
         ObjectSetInteger(0, lab, OBJPROP_FONTSIZE, 8);
         ObjectSetString(0, lab, OBJPROP_FONT, "Consolas");
         ObjectSetInteger(0, lab, OBJPROP_ANCHOR, ANCHOR_LEFT_UPPER);
         ObjectSetInteger(0, lab, OBJPROP_SELECTABLE, false);
         ObjectSetInteger(0, lab, OBJPROP_HIDDEN, true);
      }

      drawn++;
   }
   return drawn;
}

//+------------------------------------------------------------------+
void CreateHLine(const string name, const double price, const color clr, const ENUM_LINE_STYLE style, const int width)
{
   if(!ObjectCreate(0, name, OBJ_HLINE, 0, 0, price))
      ObjectSetDouble(0, name, OBJPROP_PRICE, price);
   ObjectSetInteger(0, name, OBJPROP_COLOR, clr);
   ObjectSetInteger(0, name, OBJPROP_STYLE, style);
   ObjectSetInteger(0, name, OBJPROP_WIDTH, width);
   ObjectSetInteger(0, name, OBJPROP_BACK, true);
   ObjectSetInteger(0, name, OBJPROP_SELECTABLE, false);
   ObjectSetInteger(0, name, OBJPROP_HIDDEN, true);
   ObjectSetString(0, name, OBJPROP_TEXT, "");
}

//+------------------------------------------------------------------+
void DeleteNtbotZoneObjects()
{
   int total = ObjectsTotal(0, 0, -1);
   for(int i = total - 1; i >= 0; i--)
   {
      string name = ObjectName(0, i, 0, -1);
      if(StringFind(name, NTBOT_OZ_PREFIX) == 0)
         ObjectDelete(0, name);
   }
}

//+------------------------------------------------------------------+
color RgbStringToColor(const string rgb)
{
   string parts[];
   if(StringSplit(rgb, ',', parts) < 3)
      return clrDodgerBlue;
   int r = (int)StringToInteger(parts[0]);
   int g = (int)StringToInteger(parts[1]);
   int b = (int)StringToInteger(parts[2]);
   return (color)((b << 16) | (g << 8) | r); // MT5 COLOR is BGR
}

//+------------------------------------------------------------------+
color SoftPaletteColor(const string kind, const string side, const color fallback)
{
   // Compra = verde claro | Venda = vermelho claro (força paleta local).
   const string buyRgb  = "120,230,150"; // verde claro
   const string sellRgb = "255,140,145"; // vermelho claro
   if(kind == "demand" || kind == "ob_buy" || kind == "discount" || kind == "target")
      return RgbStringToColor(buyRgb);
   if(kind == "supply" || kind == "ob_sell" || kind == "premium")
      return RgbStringToColor(sellRgb);
   if(kind == "fvg")
      return (side == "sell") ? RgbStringToColor(sellRgb) : RgbStringToColor(buyRgb);
   if(kind == "liquidity")
      return (side == "sell") ? RgbStringToColor(sellRgb) : RgbStringToColor(buyRgb);
   if(kind == "poc" || kind == "ote")
      return RgbStringToColor("230,210,140");
   if(kind == "vwap")
      return RgbStringToColor("140,200,230");
   if(side == "sell")
      return RgbStringToColor(sellRgb);
   if(side == "buy")
      return RgbStringToColor(buyRgb);
   return fallback;
}

//+------------------------------------------------------------------+
string LocalizeLabel(const string raw, const string kind, const string side)
{
   // Remap EN / corrupted glyphs → PT ASCII (keeps trailing price range).
   string label = raw;
   StringReplace(label, "Demand", "Demanda");
   StringReplace(label, "Supply", "Oferta");
   StringReplace(label, "Premium", "Premio");
   StringReplace(label, "Discount", "Desconto");
   StringReplace(label, "Target", "Alvo");
   StringReplace(label, "Liq ", "Liquidez ");
   StringReplace(label, "Value Area", "Area de valor");
   // Corrupted UTF-8 arrows often render as junk after FVG/Liq — normalize by kind.
   if(kind == "fvg")
   {
      string range = ExtractPriceRangeSuffix(label);
      label = (side == "sell" ? "FVG-" : "FVG+") + range;
   }
   else if(kind == "liquidity")
   {
      string range = ExtractPriceRangeSuffix(label);
      bool up = (StringFind(label, "topo") >= 0 || StringFind(raw, "+") >= 0);
      label = (up ? "Liquidez+" : "Liquidez-") + range;
   }
   else if(kind == "demand" || kind == "ob_buy")
   {
      string range = ExtractPriceRangeSuffix(label);
      if(StringFind(label, "Demanda") < 0)
         label = "Demanda" + range;
   }
   else if(kind == "supply" || kind == "ob_sell")
   {
      string range = ExtractPriceRangeSuffix(label);
      if(StringFind(label, "Oferta") < 0)
         label = "Oferta" + range;
   }
   else if(kind == "premium")
   {
      string range = ExtractPriceRangeSuffix(label);
      label = "Premio" + range;
   }
   else if(kind == "discount")
   {
      string range = ExtractPriceRangeSuffix(label);
      label = "Desconto" + range;
   }
   else if(kind == "target")
   {
      string range = ExtractPriceRangeSuffix(label);
      label = "Alvo" + range;
   }
   return label;
}

//+------------------------------------------------------------------+
string ExtractPriceRangeSuffix(const string label)
{
   // Keep " 4282.76-4297.77" / " 4307.72" tail when present.
   int sp = -1;
   for(int i = StringLen(label) - 1; i >= 0; i--)
   {
      ushort ch = StringGetCharacter(label, i);
      if(ch == ' ')
      {
         string tail = StringSubstr(label, i);
         // crude: tail starts with space + digit
         if(StringLen(tail) > 2)
         {
            ushort d = StringGetCharacter(tail, 1);
            if(d >= '0' && d <= '9')
               return tail;
         }
         break;
      }
   }
   return "";
}

//+------------------------------------------------------------------+
string SanitizeLabel(const string raw)
{
   // Drop non-ASCII bytes left by ANSI mis-decode of UTF-8 arrows.
   string out = "";
   int n = StringLen(raw);
   for(int i = 0; i < n; i++)
   {
      ushort ch = StringGetCharacter(raw, i);
      if(ch >= 32 && ch < 127)
         out += CharToString((uchar)ch);
      else if(ch == 0x00)
         break;
   }
   StringTrimLeft(out);
   StringTrimRight(out);
   return out;
}

//+------------------------------------------------------------------+
color SoftFillColor(const color clr, const int alpha)
{
   // Light tint on dark chart: keep candles readable, show green/red clearly.
   double strength = MathMax(0.18, MathMin(0.38, (alpha / 255.0) * 1.35));
   int baseR = 22, baseG = 24, baseB = 28;
   int r = (int)(baseR * (1.0 - strength) + (clr & 0xFF) * strength);
   int g = (int)(baseG * (1.0 - strength) + ((clr >> 8) & 0xFF) * strength);
   int b = (int)(baseB * (1.0 - strength) + ((clr >> 16) & 0xFF) * strength);
   return (color)((b << 16) | (g << 8) | r);
}

//+------------------------------------------------------------------+
color SoftEdgeColor(const color clr)
{
   // Edges/labels follow buy green / sell red (slightly brighter than fill).
   double t = 0.72;
   int r = (int)((clr & 0xFF) * t + 40.0 * (1.0 - t));
   int g = (int)(((clr >> 8) & 0xFF) * t + 40.0 * (1.0 - t));
   int b = (int)(((clr >> 16) & 0xFF) * t + 40.0 * (1.0 - t));
   return (color)((b << 16) | (g << 8) | r);
}

//+------------------------------------------------------------------+
// Extract "field":"value" from a flat JSON object (handles escaped newlines poorly —
// prefer delim field which we also unescape).
string ExtractJsonStringField(const string json, const string field)
{
   string key = "\"" + field + "\":\"";
   int start = StringFind(json, key);
   if(start < 0)
      return "";
   start += StringLen(key);
   int end = start;
   int len = StringLen(json);
   while(end < len)
   {
      ushort ch = StringGetCharacter(json, end);
      if(ch == '"' && StringGetCharacter(json, end - 1) != '\\')
         break;
      end++;
   }
   if(end <= start)
      return "";
   string value = StringSubstr(json, start, end - start);
   StringReplace(value, "\\n", "\n");
   StringReplace(value, "\\\"", "\"");
   StringReplace(value, "\\\\", "\\");
   return value;
}

string ExtractJsonStringFieldRaw(const string json, const string field)
{
   return ExtractJsonStringField(json, field);
}

//+------------------------------------------------------------------+
long ExtractJsonIntField(const string json, const string field)
{
   string key = "\"" + field + "\":";
   int start = StringFind(json, key);
   if(start < 0)
      return -1;
   start += StringLen(key);
   while(start < StringLen(json) && StringGetCharacter(json, start) == ' ')
      start++;
   string num = "";
   int end = start;
   while(end < StringLen(json))
   {
      ushort ch = StringGetCharacter(json, end);
      if((ch < '0' || ch > '9') && ch != '-')
         break;
      num += CharToString((uchar)ch);
      end++;
   }
   if(num == "")
      return -1;
   return StringToInteger(num);
}
//+------------------------------------------------------------------+
