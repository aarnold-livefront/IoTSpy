#!/bin/sh
# ADM App Central portal redirect — opens IoTSpy UI in a new tab.
# The NAS_HOST variable is read from the install .env if available.

ENV_FILE="/volume1/IoTSpy/.env"
NAS_HOST="localhost"

if [ -f "${ENV_FILE}" ]; then
    VAL="$(grep '^NAS_HOST=' "${ENV_FILE}" | cut -d= -f2 | tr -d '[:space:]')"
    [ -n "$VAL" ] && NAS_HOST="$VAL"
fi

printf "Content-Type: text/html\r\n\r\n"
printf '<meta http-equiv="refresh" content="0;url=http://%s:5000/">\n' "${NAS_HOST}"
