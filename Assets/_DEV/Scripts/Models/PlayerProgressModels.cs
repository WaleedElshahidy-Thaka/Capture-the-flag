using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgress
{
    public int coins;
    public int chips;
    public int energy;
    public int maxEnergy;
    public int regenRate;
    public int level;
    public int experience;
    public int maxExperience;
    public List<string> userItems;
    public DailyRewardsBundles dailyRewardsBundles;
}

[Serializable]
public class DailyRewardsBundles
{
    public int currentDay;
    public DailyRewardBundle dailyRewardBundle;
}

[Serializable]
public class DailyRewardBundle
{
    public string name;
    public int totalDays;
    public bool isDefault;
    public bool isPremium;
    public List<DailyRewardDay> days;
}

[Serializable]
public class DailyRewardDay
{
    public int  day;
    public bool claimed;
    public List<DailyReward> rewards;
}

[Serializable]
public class DailyReward
{
    public string type;
    public int    amount;
}
