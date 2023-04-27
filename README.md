# StockDailyReport
Aplikacja Windows Service - StockDailyReport
1. Apkikacja codziennie o godzinie 22 (po zamknięciu giełdy w USA wysyła raport email i sms do użytkownika z kursem wybranej spółki.
2. W aplikacji ustawiłem kurs otwarcia i zamknięcia z danego dnia. Można to zmieniać dowolnie, w zależności od dostawcy danych.
3. Domyślnie ustawione jest pobieranie danych dla spólki Apple ('AAPL').
4. Źródło danych giełdowych - https://marketstack.com
5. Serwis SMS - https://www.twilio.com
6. W aplikacji błędy logują się do oddzielnego pliku o nawie {data}_errors.log
