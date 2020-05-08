# CSaN_LR4
## Лабораторная работа №4
Необходимо реализовать простой прокси-сервер, выполняющий журналирование проксируемых HTTP-запросов.

Программа должна работать в виде службы и отображать в виде журнала краткую информацию о проксируемых запросах (URL и код ответа). *При реализации использовать программный интерфейс сокетов.*

**Обязательной является поддержка HTTP, поддержка HTTPS не требуется.**

Для проверки работоспособности необходимо настроить в браузере работу через прокси и попробовать загрузить ресурсы по HTTP:
* http://example.com/
* http://live.legendy.by:8000/legendyfm - онлайн радио, необходимо для проверки, что соединение не разрывается раньше времени

### Дополнительное задание
Реализовать фильтрацию сайтов по черному списку. В конфигурационном файле для прокси-сервера задается список доменов и/или URL-адресов для блокировки.

При попытке загрузить страницу из черного списка прокси-сервер должен вернуть предопределенную страницу с адресом, доступ к которому заблокирован, и сообщением об ошибке.
