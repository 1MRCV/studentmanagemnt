pipeline {
    agent { label 'windows-agent' }

    parameters {
        choice(
            name: 'ENVIRONMENT',
            choices: ['DEV', 'PRODUCTION'],
            description: 'Select environment'
        )

        choice(
            name: 'ACTION',
            choices: ['DEPLOY', 'ROLLBACK'],
            description: 'Deploy or rollback'
        )

        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch (DEV only)'
        )

        string(
            name: 'ARTIFACT_BUILD',
            defaultValue: '',
            description: 'Used only for PRODUCTION'
        )
    }

    environment {
        DEV_ARTIFACT_STORAGE = "C:\\jenkins-artifacts\\DEV"
    }

    stages {

        // =========================
        // DEV ONLY - CHECKOUT
        // =========================
        stage('Checkout Code') {
            when {
                expression { params.ENVIRONMENT == 'DEV' }
            }
            steps {
                deleteDir()
                git branch: "${params.BRANCH}", url: 'https://github.com/1MRCV/studentmanagemnt.git'
            }
        }

        // =========================
        // DEV ONLY - BUILD
        // =========================
        stage('Build + Publish') {
            when {
                expression { params.ENVIRONMENT == 'DEV' }
            }
            steps {
                powershell '''
                dotnet restore
                dotnet build --configuration Release
                dotnet publish -c Release -o publish
                '''
            }
        }

        // =========================
        // DEV ONLY - CREATE ARTIFACT
        // =========================
        stage('Create Artifact') {
            when {
                expression { params.ENVIRONMENT == 'DEV' }
            }
            steps {
                powershell '''
                $date = Get-Date -Format "yyyy-MM-dd"
                $buildNum = $env:BUILD_NUMBER

                $artifactPath = "$env:DEV_ARTIFACT_STORAGE\\build_${buildNum}_$date"

                New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
                Copy-Item "publish\\*" $artifactPath -Recurse -Force

                Write-Host "Artifact created: $artifactPath"
                '''
            }
        }

        // =========================
        // DEPLOY (DEV + PROD)
        // =========================
        stage('Deploy') {
            when {
                expression { params.ACTION == 'DEPLOY' }
            }
            steps {
                powershell '''

                if ("${params.ENVIRONMENT}" -eq "DEV") {

                    Write-Host "DEV: Using fresh build"
                    $source = "publish"
                }
                else {

                    if ("${params.ARTIFACT_BUILD}" -eq "" -or "${params.ARTIFACT_BUILD}" -like "-- Select Build --") {
                        throw "Select ARTIFACT_BUILD for PRODUCTION"
                    }

                    $buildNum = "${params.ARTIFACT_BUILD}".Split("|")[0].Replace("Build_","").Trim()

                    $folder = Get-ChildItem "$env:DEV_ARTIFACT_STORAGE" -Directory |
                              Where-Object { $_.Name -like "build_${buildNum}_*" } |
                              Select-Object -First 1

                    if ($null -eq $folder) {
                        throw "Artifact not found for build $buildNum"
                    }

                    Write-Host "PROD: Using artifact build $buildNum"
                    $source = $folder.FullName
                }

                $destination = "C:\\inetpub\\wwwroot\\StudentPortal"

                if (!(Test-Path $source)) {
                    throw "Source not found: $source"
                }

                iisreset /stop
                Remove-Item "$destination\\*" -Recurse -Force -ErrorAction SilentlyContinue
                Copy-Item "$source\\*" $destination -Recurse -Force
                iisreset /start

                Write-Host "Deployment SUCCESS"
                '''
            }
        }

        // =========================
        // ROLLBACK (PROD ONLY)
        // =========================
        stage('Rollback') {
            when {
                expression { params.ACTION == 'ROLLBACK' && params.ENVIRONMENT == 'PRODUCTION' }
            }
            steps {
                powershell '''

                if ("${params.ARTIFACT_BUILD}" -eq "") {
                    throw "Select ARTIFACT_BUILD for rollback"
                }

                $buildNum = "${params.ARTIFACT_BUILD}".Split("|")[0].Replace("Build_","").Trim()

                $folder = Get-ChildItem "$env:DEV_ARTIFACT_STORAGE" -Directory |
                          Where-Object { $_.Name -like "build_${buildNum}_*" } |
                          Select-Object -First 1

                if ($null -eq $folder) {
                    throw "Artifact not found for rollback build $buildNum"
                }

                $destination = "C:\\inetpub\\wwwroot\\StudentPortal"

                iisreset /stop
                Remove-Item "$destination\\*" -Recurse -Force -ErrorAction SilentlyContinue
                Copy-Item "$folder.FullName\\*" $destination -Recurse -Force
                iisreset /start

                Write-Host "Rollback SUCCESS"
                '''
            }
        }

        stage('Verify') {
            steps {
                echo "Pipeline completed"
            }
        }
    }

    post {
        failure {
            echo "FAILED"
        }
    }
}
