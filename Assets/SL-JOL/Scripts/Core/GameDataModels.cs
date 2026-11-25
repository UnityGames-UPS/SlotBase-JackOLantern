using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#region Server Communication Models

[Serializable]
public class InitData
{
    public string id = "initData";
    public ServerGameData gameData;
    public ServerFeatures features;
    public ServerUIData uiData;
    public ServerPlayer player;
    public JackpotData jackpotData;
}

[Serializable]
public class JackpotData
{
    public JackpotValues values;//jackpotfeature

}

[Serializable]
public class JackpotValues
{
    public string miniJackpot;
    public string minorJackpot;
    public string majorJackpot;
    public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
    public string gameId;
    public JackpotValues values;
}

[Serializable]
public class ServerGameData
{
    public List<List<int>> lines;
    public List<double> bets;
    public int totalLines;
}

[Serializable]
public class ServerFeatures
{
    public USpinFeature uSpin;
    public MoneyBagFeature moneyBag;
    public FreeGamesFeature freeGames;
    public int betMultiplier;
    public int maxWinMultiplier;
    public int minWinMultiplier;
}

[Serializable]
public class USpinFeature
{
    public bool enabled;
    public int minTrigger;
    public int symbolId;
    public List<USpinSegment> segments;
}

[Serializable]
public class USpinSegment
{
    public int sliceIndex;
    public string type;
    public double multiplier;
    public int freeGames;
}

[Serializable]
public class MoneyBagFeature
{
    public bool enabled;
    public int minTrigger;
    public int symbolId;
    public int bagCount;
}

[Serializable]
public class FreeGamesFeature
{
    public bool enabled;
    public bool retrigger;
    public double multiplier = 1;
    public int spinsCount;
    public int maxFreeSpins;
    public int triggerCount;
    public int extraWildCount;
    public int wildStackHeight;
    public int retriggerSpinsCount;
}

[Serializable]
public class ExtraSpinsData
{
    [JsonProperty("2")] public int _2; // Keep for safety/compatibility with UI
    [JsonProperty("3")] public int _3;
    [JsonProperty("4")] public int _4;
    [JsonProperty("5")] public int _5;
}

[Serializable]
public class ServerUIData
{
    public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
    public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
    public int id;
    public string name;
    public List<double> multiplier;
    public string description;
}


[Serializable]
public class ServerPlayer
{
    public double balance;
}

// ============================================================================
// FIXED: Server Response Models - Must match actual server JSON structure
// ============================================================================

[Serializable]
public class ServerSpinResponse
{
    public string id = "ResultData";
    public bool success;
    public List<List<string>> matrix; // Root level matrix sent by server
    public ServerPlayerBalance player;
    public ServerPayload payload;
}

[Serializable]
public class ServerPlayerBalance
{
    public double? balance; // Nullable because server sends null
}

[Serializable]
public class ServerPayload
{
    public double winAmount;
    public List<ServerLineWin> lineWins;
    [JsonProperty("scatterWinAmount")]
    public double scatterWin;
    public double? outBalance;
    public int activeLines;
    public int freeSpinsRemaining;
    public bool isFreeSpinActive;
    [JsonProperty("isFreeSpinTriggered")]
    public bool isFreeSpinsTriggered;
    public int freeSpinsAwarded;
    public ServerFreeGamesState freeGames;
    public double globalMultiplier = 1;
}

[Serializable]
public class ServerFreeGamesState
{
    public bool triggered;
    public bool retriggered;
    public bool active;
    public bool isCompleted;
    public int spinsRemaining;
    public int totalSpins;
    public double? totalWinCash;
    public double? gameWin;
    public double? featureWin;
    public double? totalWin;
    public double multiplier = 1;
}

[Serializable]
public class ServerLineWin
{
    public int lineIndex;
    public List<ServerLineWinPosition> positions;
    public string symbolId;
    public string symbolName;
    public double payout;
    public int matchCount;
}

[Serializable]
public class ServerLineWinPosition
{
    // Server format: [row, column]
    public List<int> position;
}

[Serializable]
public class ServerUSpinResult
{
    public bool triggered;
    public ServerUSpinResultDetail result;
}

[Serializable]
public class ServerUSpinResultDetail
{
    public int sliceIndex;
    public string type;
    public double multiplierAwarded;
    public int freeGamesAwarded;
    public double winInCash;
}

[Serializable]
public class ServerMoneyBagResult
{
    public bool triggered;
    public ServerMoneyBagResultDetail result;
}

[Serializable]
public class ServerMoneyBagResultDetail
{
    public int pickedIndex;
    public List<int> revealed;
    public int creditsAwarded;
    public double winInCash;
}

[Serializable]
public class ServerFreeGamesResult
{
    public bool triggered;
    public int totalAwarded;
    public int played;
    public double totalFreeGamesWin;
}

// ============================================================================
// Client-Side Spin Request
// ============================================================================

[Serializable]
public class SpinRequest
{
    public string type = "SPIN";
    public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
    public int betIndex;
    public bool isFreeSpin;
}

[Serializable]
public class JackpotOpenRequest
{
    public string type = "JACKPOT_OPEN";
    public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
  public string tier;
}


#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
    public int reelCount = 5;
    public int rowCount = 4;
    public int symbolCount = 12;
    public int paylineCount = 40;
    public List<List<int>> paylines;
    public List<double> availableBets;
    public List<SymbolInfo> symbols;

    public int wildSymbolId = 0;
    public int scatterSymbolId = 1;
    public int initialFreeSpins = 8;
    public FreeGamesFeature freeGames;
}

[Serializable]
public class SymbolInfo
{
    public int id;
    public string name;
    public string description;
    public List<double> multipliers;
    public bool isWild;
    public bool isScatter;
    public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
    public double balance;
    public int currentBetIndex;
}

[Serializable]
public class SpinResult
{
    public List<List<int>> resultMatrix;  // Client uses int matrix
    public double winAmount;
    public List<WinLine> winLines;
    public PlayerData playerData;
    public FreeSpinData freeSpinData;
    public ScatterData scatterData;
    public ServerFreeGamesState freeGames;
    public int serverSpinsRemaining;
    public bool isFreeSpinActive;
    public int activeLines;
    public double scatterWin;
    public double globalMultiplier = 1;
}

[Serializable]
public class WinLine
{
    public int lineId;
    public int symbolId;
    public List<int> positions;  // Flat list: [row * 5 + col]
    public double winAmount;
}

[Serializable]
public class FreeSpinData
{
    public bool isTriggered;
    public int spinsAwarded;
    public int remainingSpins;
    public bool isBought;
}

[Serializable]
public class ScatterData
{
    public bool isTriggered;
    public int scatterCount;
    public double winAmount;
}

[Serializable]
public class OverlayScatterData
{
    public bool isTriggered;
    public int count;
    public int extraSpins;
    public List<List<int>> positions;
}

[Serializable]
public class USpinResultData
{
    public bool triggered;
    public int sliceIndex;
    public string type;
    public double multiplierAwarded;
    public int freeGamesAwarded;
    public double winInCash;
}

[Serializable]
public class MoneyBagResultData
{
    public bool triggered;
    public int pickedIndex;
    public List<int> revealed;
    public int creditsAwarded;
    public double winInCash;
}

#endregion

#region Platform Communication

[Serializable]
public class AuthData
{
    public string token;
    public string socketURL;
    public string nameSpace;
}

#endregion

#region Enums

public enum GameState
{
    Initializing,
    Idle,
    Spinning,
    Stopping,
    ShowingWin,
    FreeSpinMode
}

public enum SpinSpeed
{
    Normal,
    Turbo,
    QuickSpin
}

public enum WinPopupType
{
    RegularWin = 0,       // Normal credit win
    FreeSpinTrigger = 2,  // Free spins awarded by Scatter symbols
    MoneyBagCollect = 3,  // Money bag feature collect
    FreeSpinComplete = 4  // All free spins completed
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// Converts server data to client GameConfig
/// </summary>
public static class InitDataConverter
{
    internal static GameConfig ConvertToGameConfig(InitData serverData)
    {
        if (serverData?.gameData == null || serverData.uiData?.paylines?.symbols == null)
        {
            throw new ArgumentException("Initialization payload is missing gameData or uiData.paylines.symbols.");
        }

        int rowCount = 4;
        if (serverData.gameData.lines != null && serverData.gameData.lines.Count > 0)
        {
            rowCount = serverData.gameData.lines
                .Where(line => line != null && line.Count > 0)
                .SelectMany(line => line)
                .DefaultIfEmpty(3)
                .Max() + 1;
        }

        var config = new GameConfig
        {
            reelCount = 5,
            rowCount = rowCount,
            symbolCount = serverData.uiData.paylines.symbols.Count,
            paylineCount = serverData.gameData.totalLines > 0
                ? serverData.gameData.totalLines
                : serverData.gameData.lines?.Count ?? 40,
            paylines = serverData.gameData.lines,
            availableBets = serverData.gameData.bets,
            symbols = new List<SymbolInfo>(),
            initialFreeSpins = serverData.features?.freeGames?.spinsCount ?? 8,
            freeGames = serverData.features?.freeGames
        };

        foreach (var serverSymbol in serverData.uiData.paylines.symbols)
        {
            string symbolName = serverSymbol.name ?? string.Empty;
            bool isWild = symbolName.IndexOf("wild", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isScatter = symbolName.IndexOf("scatter", StringComparison.OrdinalIgnoreCase) >= 0;
            var reversedMultipliers = serverSymbol.multiplier != null
                ? serverSymbol.multiplier.AsEnumerable().Reverse().ToList()
                : new List<double>();

            var symbolInfo = new SymbolInfo
            {
                id = serverSymbol.id,
                name = symbolName,
                description = serverSymbol.description,
                multipliers = reversedMultipliers,
                isWild = isWild,
                isScatter = isScatter,
                minMatch = reversedMultipliers.Count > 0 ? 6 - reversedMultipliers.Count : 0
            };

            config.symbols.Add(symbolInfo);

            if (isWild) config.wildSymbolId = symbolInfo.id;
            if (isScatter) config.scatterSymbolId = symbolInfo.id;
        }

        return config;
    }

    internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
    {
        return new PlayerData
        {
            balance = serverPlayer.balance,
            currentBetIndex = defaultBetIndex
        };
    }

    /// <summary>
    /// Converts server response to client SpinResult
    /// </summary>
    internal static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, GameConfig gameConfig)
    {
        if (serverResponse?.payload == null)
        {
            throw new ArgumentException("Spin response is missing payload data.");
        }

        double winAmount = serverResponse.payload.winAmount;
        double newBalance = serverResponse.player?.balance
            ?? serverResponse.payload.outBalance
            ?? CalculateNewBalance(currentBalance, winAmount);
        List<List<int>> resultMatrix = ConvertMatrix(serverResponse.matrix, gameConfig);
        List<WinLine> winLines = ConvertWinningLines(serverResponse.payload.lineWins, gameConfig);
        int scatterCount = CountSymbol(resultMatrix, gameConfig?.scatterSymbolId ?? 1);
        FreeSpinData freeSpinData = ConvertFreeSpinData(serverResponse.payload);
        bool isFreeSpinTriggered = freeSpinData != null && freeSpinData.isTriggered;

        if (serverResponse.payload.scatterWin > 0 && scatterCount > 0)
        {
            winLines.Add(new WinLine
            {
                lineId = -1,
                symbolId = gameConfig?.scatterSymbolId ?? 1,
                positions = FindSymbolPositions(resultMatrix, gameConfig?.scatterSymbolId ?? 1),
                winAmount = serverResponse.payload.scatterWin
            });
        }

        var result = new SpinResult
        {
            resultMatrix = resultMatrix,
            winAmount = winAmount,
            winLines = winLines,

            playerData = new PlayerData
            {
                balance = newBalance,
                currentBetIndex = 0
            },

            freeSpinData = freeSpinData,

            scatterData = (serverResponse.payload.scatterWin > 0 || isFreeSpinTriggered)
                ? new ScatterData
                {
                    isTriggered = isFreeSpinTriggered,
                    scatterCount = scatterCount,
                    winAmount = serverResponse.payload.scatterWin
                }
                : null,

            freeGames = serverResponse.payload.freeGames,
            serverSpinsRemaining = serverResponse.payload.freeGames?.isCompleted == true
                ? 0
                : Math.Max(0, serverResponse.payload.freeGames?.spinsRemaining
                    ?? serverResponse.payload.freeSpinsRemaining),
            isFreeSpinActive = serverResponse.payload.freeGames != null
                ? serverResponse.payload.freeGames.active && !serverResponse.payload.freeGames.isCompleted
                : serverResponse.payload.isFreeSpinActive,
            activeLines = serverResponse.payload.activeLines,
            scatterWin = serverResponse.payload.scatterWin,
            globalMultiplier = serverResponse.payload.globalMultiplier > 0
                ? serverResponse.payload.globalMultiplier
                : 1
        };

        return result;
    }

    private static FreeSpinData ConvertFreeSpinData(ServerPayload payload)
    {
        ServerFreeGamesState state = payload.freeGames;
        bool triggered = state != null
            ? state.triggered || state.retriggered
            : payload.isFreeSpinsTriggered;

        if (!triggered) return null;

        return new FreeSpinData
        {
            isTriggered = true,
            // Retrigger awards are the change in totalSpins, resolved against
            // the previous session total by GameManager.
            spinsAwarded = state == null
                ? Math.Max(0, payload.freeSpinsAwarded)
                : state.retriggered
                    ? 0
                    : Math.Max(0, state.totalSpins),
            remainingSpins = Math.Max(0, state?.spinsRemaining ?? payload.freeSpinsRemaining),
            isBought = false
        };
    }

    private static List<List<int>> ConvertMatrix(List<List<string>> serverMatrix, GameConfig gameConfig)
    {
        int rowCount = gameConfig != null ? gameConfig.rowCount : 4;

        if (serverMatrix == null || serverMatrix.Count == 0)
        {
            UnityEngine.Debug.LogError("Invalid server matrix: matrix is null or empty");
            return GenerateDefaultMatrix(rowCount);
        }

        int totalRows = serverMatrix.Count;
        int totalCols = serverMatrix[0]?.Count ?? 0;

        var matrix = new List<List<int>>();

        for (int col = 0; col < totalCols; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < totalRows; row++)
            {
                if (serverMatrix[row] == null || col >= serverMatrix[row].Count)
                {
                    UnityEngine.Debug.LogError($"Invalid server data at row {row}, col {col}");
                    column.Add(0);
                    continue;
                }

                string symbolStr = serverMatrix[row][col];
                if (!int.TryParse(symbolStr, out int symbolId))
                {
                    UnityEngine.Debug.LogError($"Failed to parse symbol: {symbolStr}");
                    column.Add(0);
                    continue;
                }

                column.Add(symbolId);
            }
            matrix.Add(column);
        }

        return matrix;
    }

    private static List<List<int>> GenerateDefaultMatrix(int rowCount)
    {
        var matrix = new List<List<int>>();
        for (int col = 0; col < 5; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(0);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    private static List<WinLine> ConvertWinningLines(List<ServerLineWin> serverLineWins, GameConfig gameConfig)
    {
        var winLines = new List<WinLine>();
        if (serverLineWins == null) return winLines;

        int reelCount = gameConfig?.reelCount ?? 5;
        foreach (var serverLineWin in serverLineWins)
        {
            var flatPositions = new List<int>();
            if (serverLineWin.positions != null)
            {
                foreach (var positionData in serverLineWin.positions)
                {
                    if (positionData?.position == null || positionData.position.Count < 2) continue;

                    int row = positionData.position[0];
                    int column = positionData.position[1];
                    flatPositions.Add(row * reelCount + column);
                }
            }

            int.TryParse(serverLineWin.symbolId, out int symbolId);

            winLines.Add(new WinLine
            {
                lineId = serverLineWin.lineIndex,
                symbolId = symbolId,
                positions = flatPositions,
                winAmount = serverLineWin.payout
            });
        }

        return winLines;
    }

    private static int CountSymbol(List<List<int>> matrix, int symbolId)
    {
        if (matrix == null) return 0;
        return matrix.Sum(column => column?.Count(symbol => symbol == symbolId) ?? 0);
    }

    private static List<int> FindSymbolPositions(List<List<int>> matrix, int symbolId)
    {
        var positions = new List<int>();
        if (matrix == null) return positions;

        int reelCount = matrix.Count;
        for (int column = 0; column < matrix.Count; column++)
        {
            if (matrix[column] == null) continue;
            for (int row = 0; row < matrix[column].Count; row++)
            {
                if (matrix[column][row] == symbolId)
                {
                    positions.Add(row * reelCount + column);
                }
            }
        }

        return positions;
    }

    private static double CalculateNewBalance(double currentBalance, double winAmount)
    {
        // Paid spins are deducted optimistically when the spin starts. Free spins
        // have no deduction, so the same fallback is correct for both modes.
        return currentBalance + winAmount;
    }
}

#endregion
