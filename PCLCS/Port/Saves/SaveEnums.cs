namespace PCLCS.Port.Saves;

/// <summary>游戏模式。与 Minecraft level.dat 中 Data.GameType 取值一致。</summary>
public enum GameMode
{
    Survival = 0,
    Creative = 1,
    Adventure = 2,
    Spectator = 3
}

/// <summary>游戏难度。level.dat 中 Data.Difficulty（旧版本）取值。</summary>
public enum Difficulty
{
    Peaceful = 0,
    Easy = 1,
    Normal = 2,
    Hard = 3
}
