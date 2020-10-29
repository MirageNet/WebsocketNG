#!/bin/bash

# https://community.axway.com/s/question/0D52X000065Ykx2SAC/example-scripts-to-create-certificate-chain-with-openssl

#Generate CA Certificate

#Generate private Key
openssl genrsa -out CA.key 2048

#Generate CA CSR
openssl req -new -sha256 -key CA.key -out CA.csr -subj "/C=BR/ST=SAO PAULO/L=SAO PAULO/O=MirrorNG/CN=CA CERTIFICATE"

#Generate CA Certificate (100 years)
openssl x509 -signkey CA.key -in CA.csr -req -days 36500 -out CA.pem

#--------------------------------------------------------------------------------------

#Generate Intermediary CA Certificate

#Generate private Key
openssl genrsa -out CA_Intermediary.key 2048

#Create Intermediary CA CSR
openssl req -new -sha256 -key CA_Intermediary.key -out CA_Intermediary.csr -subj "/C=BR/ST=SAO PAULO/L=SAO PAULO/O=MirrorNG/CN=CA INTERMEDIARY CERTIFICATE"

#Generate Server Certificate (100 years)
openssl x509 -req -in CA_Intermediary.csr -CA CA.pem -CAkey CA.key -CAcreateserial -out CA_Intermediary.crt -days 36500 -sha256

#--------------------------------------------------------------------------------------

#Generate Server Certificate signed by Intermediary CA

#Generate private Key
openssl genrsa -out ServerCert_signedByCAIntermediary.key 2048

#Ceate Server CSR
openssl req -new -sha256 -key ServerCert_signedByCAIntermediary.key -out ServerCert_signedByCAIntermediary.csr -subj "/C=BR/ST=SAO PAULO/L=SAO PAULO/O=MirrorNG/CN=localhost/subjectAltName=DNS.1=axway.lab,DNS.2=your-alt-name"

#Generate Server Certificate
openssl x509 -req -in ServerCert_signedByCAIntermediary.csr -CA CA.pem -CAkey CA.key -CAcreateserial -out ServerCert_signedByCAIntermediary.crt -days 36500 -sha256

#View Certificate
openssl x509 -text -noout -in ServerCert_signedByCAIntermediary.crt

#Package in a pfx
openssl pkcs12 -export -out ServerCert_signedByCAIntermediary.pfx -inkey ServerCert_signedByCAIntermediary.key -in ServerCert_signedByCAIntermediary.crt -passout "pass:password"