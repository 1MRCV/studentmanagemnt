pipeline {

    agent { label 'windows-agent' }

    parameters {

        choice(
            name: 'ENVIRONMENT',
            choices: ['DEV','PRODUCTION'],
            description: 'Select deployment environment'
        )

        string(
            name: 'DEPLOY_BUILD',
            defaultValue: '',
            description: 'Build version to deploy (leave empty for latest)'
        )

        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch to build'
        )
    }

    environment {

        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

        PUBLISH_FOLDER = "publish"

        ARTIFACT_ROOT = "C:\\jenkins-artifacts"

        DEV_ARTIFACT = "C:\\jenkins-artifacts\\DEV"
        PROD_ARTIFACT = "C:\\jenkins-artifacts\\PROD"

        DEV_PATH = "C:\\inetpub\\dev"
        PROD_PATH = "C:\\inetpub\\prod"

        DEV_SITE = "student-dev"
        PROD_SITE = "student-prod"

        DEV_POOL = "student-dev-pool"
        PROD_POOL = "student-prod-pool"

        DEV_PORT = "8081"
        PROD_PORT = "8082"
    }

    stages {

        stage('Clean Workspace') {
            steps { deleteDir() }
        }

        stage('Checkout Code') {
            steps {
                git branch: "${params.BRANCH}",
                    url: "${env.GIT_REPO}"
            }
        }

        stage('Restore Dependencies') {
            steps {
                powershell 'dotnet restore'
            }
        }

        stage('Build Application') {
            steps {
                powershell 'dotnet build --configuration Release'
            }
        }

        stage('Publish Website') {
            steps {
                powershell 'dotnet publish -c Release -o $env:PUBLISH_FOLDER'
            }
        }

        stage('Create Artifact Version') {
            steps {
                powershell '''

                $build = $env:BUILD_NUMBER
                $version = "1.0.$build"
                $date = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

                $commit = git rev-parse --short HEAD
                $branch = git rev-parse --abbrev-ref HEAD

                if ($env:ENVIRONMENT -eq "DEV") {
                    $artifactRoot = "$env:DEV_ARTIFACT"
                }
                else {
                    $artifactRoot = "$env:PROD_ARTIFACT"
                }

                if (!(Test-Path $artifactRoot)) {
                    New-Item -ItemType Directory -Path $artifactRoot
                }

                $buildFolder = "$artifactRoot\\build_$build"

                if (!(Test-Path $buildFolder)) {
                    New-Item -ItemType Directory -Path $buildFolder
                }

                $zip = "$buildFolder\\artifact.zip"

                Compress-Archive `
                    -Path "$env:PUBLISH_FOLDER\\*" `
                    -DestinationPath $zip `
                    -Force

                $info = @"
Application: StudentPortal
Environment: $env:ENVIRONMENT
Version: $version
Build Number: $build
Branch: $branch
Commit: $commit
Build Date: $date
"@

                $info | Out-File "$buildFolder\\build-info.txt"

                Write-Host "Artifact stored at $buildFolder"

                '''
            }
        }

        stage('Deploy') {
            steps {
                powershell '''

                Import-Module WebAdministration

                $envName = "$env:ENVIRONMENT"
                $buildToDeploy = "$env:DEPLOY_BUILD"

                if ([string]::IsNullOrEmpty($buildToDeploy)) {
                    $buildToDeploy = "$env:BUILD_NUMBER"
                }

                if ($envName -eq "DEV") {

                    $artifactFolder = "$env:DEV_ARTIFACT\\build_$buildToDeploy"
                    $siteName = "$env:DEV_SITE"
                    $pool = "$env:DEV_POOL"
                    $path = "$env:DEV_PATH"
                    $port = "$env:DEV_PORT"

                }
                else {

                    $artifactFolder = "$env:PROD_ARTIFACT\\build_$buildToDeploy"
                    $siteName = "$env:PROD_SITE"
                    $pool = "$env:PROD_POOL"
                    $path = "$env:PROD_PATH"
                    $port = "$env:PROD_PORT"

                }

                $zipPath = "$artifactFolder\\artifact.zip"

                if (!(Test-Path $zipPath)) {
                    throw "Artifact not found: $zipPath"
                }

                Write-Host "Deploying build $buildToDeploy to $envName"

                if (!(Test-Path "IIS:\\AppPools\\$pool")) {
                    Write-Host "Creating App Pool $pool"
                    New-WebAppPool -Name $pool
                }

                if (!(Test-Path $path)) {
                    New-Item -ItemType Directory -Path $path
                }

                if (!(Test-Path "IIS:\\Sites\\$siteName")) {

                    Write-Host "Creating IIS Website $siteName"

                    New-Website `
                        -Name $siteName `
                        -Port $port `
                        -PhysicalPath $path `
                        -ApplicationPool $pool
                }

                $state = (Get-WebAppPoolState -Name $pool).Value

                if ($state -eq "Started") {
                    Stop-WebAppPool $pool
                    Start-Sleep -Seconds 2
                }

                Remove-Item "$path\\*" -Recurse -Force -ErrorAction SilentlyContinue

                Expand-Archive `
                    -Path $zipPath `
                    -DestinationPath $path `
                    -Force

                $versionFile = "$path\\version.txt"

                "Environment: $envName" | Out-File $versionFile
                "Build: $buildToDeploy" | Out-File $versionFile -Append
                "Date: $(Get-Date)" | Out-File $versionFile -Append

                Start-WebAppPool $pool
                Start-Website $siteName

                Write-Host "Deployment completed successfully"

                '''
            }
        }

        stage('Verify Deployment') {
            steps {
                powershell '''

                Import-Module WebAdministration

                if ($env:ENVIRONMENT -eq "DEV") {
                    $pool="$env:DEV_POOL"
                }
                else {
                    $pool="$env:PROD_POOL"
                }

                $state = (Get-WebAppPoolState -Name $pool).Value

                Write-Host "App Pool State: $state"

                if ($state -ne "Started") {
                    throw "Deployment verification failed"
                }

                Write-Host "Deployment verified successfully"

                '''
            }
        }
    }

    post {

        success {
            echo "Build ${BUILD_NUMBER} deployed successfully."
        }

        failure {
            echo "Deployment failed. Check logs."
        }

    }
}
