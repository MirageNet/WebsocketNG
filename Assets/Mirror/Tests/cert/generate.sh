#!/bin/bash

export PATH=/usr/local/Cellar/openssl@1.1/1.1.1g/bin:$PATH

perl /usr/local/etc/openssl@1.1/misc/CA.pl -newca
