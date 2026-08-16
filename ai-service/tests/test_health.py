"""Модуль здоровья."""

POOR_REGIME = {
    "entries": [
        {"date": "2026-08-01", "sleep_hours": 5.0, "steps": 2000, "water_ml": 800, "mood": 2},
        {"date": "2026-08-02", "sleep_hours": 5.2, "steps": 2500, "water_ml": 900, "mood": 2},
    ]
}

GOOD_REGIME = {
    "entries": [
        {"date": "2026-08-01", "sleep_hours": 8.0, "steps": 11000, "water_ml": 2500, "mood": 5},
        {"date": "2026-08-02", "sleep_hours": 7.8, "steps": 10500, "water_ml": 2400, "mood": 5},
    ]
}


def test_poor_regime_flags_risks(client, auth_headers):
    payload = client.post("/health-analysis", headers=auth_headers, json=POOR_REGIME).json()

    risks = payload["result"]["risk_factors"]
    assert len(risks) == 3, "Недосып, малая активность и нехватка воды должны быть распознаны"
    assert payload["result"]["recommendations"]


def test_good_regime_has_no_risks(client, auth_headers):
    payload = client.post("/health-analysis", headers=auth_headers, json=GOOD_REGIME).json()
    assert payload["result"]["risk_factors"] == []


def test_wellbeing_score_reflects_regime(client, auth_headers):
    """Ключевая проверка осмысленности модели: хороший режим обязан
    получить более высокую оценку, чем плохой."""
    poor = client.post("/health-analysis", headers=auth_headers, json=POOR_REGIME).json()
    good = client.post("/health-analysis", headers=auth_headers, json=GOOD_REGIME).json()

    assert good["result"]["wellbeing_score"] > poor["result"]["wellbeing_score"]
    assert good["result"]["predicted_mood"] >= poor["result"]["predicted_mood"]


def test_mood_within_valid_range(client, auth_headers):
    payload = client.post("/health-analysis", headers=auth_headers, json=GOOD_REGIME).json()
    assert 1 <= payload["result"]["predicted_mood"] <= 5
    assert 0 <= payload["result"]["wellbeing_score"] <= 100
